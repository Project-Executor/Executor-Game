using System;
using System.Collections.Generic;
using System.Linq;
using Executor.Data;

namespace Executor.Game
{
    /// <summary>판 위의 오브젝트 하나 (map_data의 objects 항목 + 진행 중 상태)</summary>
    public class BoardObject
    {
        public string Id { get; }
        /// <summary>MapObjectTypes 상수</summary>
        public string Type { get; }
        public GridPos Pos { get; internal set; }
        /// <summary>breakable의 남은 내구도</summary>
        public int Hp { get; internal set; }
        public string KeyId { get; }
        public string Name { get; }
        public string DialogueId { get; }
        /// <summary>부서진 블록, 주운 열쇠, 열린 문은 true. 더는 칸을 차지하지 않는다</summary>
        public bool IsRemoved { get; internal set; }

        internal BoardObject(MapObjectData data)
        {
            Id = data.Id;
            Type = data.Type;
            Pos = new GridPos(data.X, data.Y);
            Hp = data.Hp ?? 1;
            KeyId = data.KeyId;
            Name = data.Name;
            DialogueId = data.DialogueId;
        }
    }

    public enum MoveOutcome
    {
        /// <summary>벽, 인물, 밀 수 없는 상자, 열쇠 없는 문. 턴을 쓰지 않는다</summary>
        Blocked,
        Moved,
        /// <summary>상자를 밀었다. 플레이어는 제자리</summary>
        Pushed,
        /// <summary>블록 내구도가 줄었다. 플레이어는 제자리</summary>
        Damaged,
        /// <summary>블록이 부서졌다. 플레이어는 제자리</summary>
        Broke,
        PickedUpKey,
        OpenedDoor,
        Cleared
    }

    /// <summary>입력 하나의 결과. 화면은 이 값을 보고 애니메이션·효과음을 정한다</summary>
    public readonly struct MoveResult
    {
        public MoveOutcome Outcome { get; }
        public Direction Direction { get; }
        public GridPos PlayerFrom { get; }
        public GridPos PlayerTo { get; }
        /// <summary>영향을 받은 오브젝트 (상자·블록·열쇠·문·출구·막은 인물). 바닥 이동이나 벽이면 null</summary>
        public BoardObject Target { get; }

        public bool UsedTurn => Outcome != MoveOutcome.Blocked;

        public MoveResult(MoveOutcome outcome, Direction direction, GridPos from, GridPos to, BoardObject target)
        {
            Outcome = outcome;
            Direction = direction;
            PlayerFrom = from;
            PlayerTo = to;
            Target = target;
        }
    }

    /// <summary>
    /// 한 판의 상태와 이동 규칙 (docs/map-data.md 3장). Unity에 의존하지 않는다.
    /// 다시 하기는 새 Board를 만든다.
    /// </summary>
    public class Board
    {
        private readonly string[] _tiles;
        private readonly List<BoardObject> _objects;
        private readonly Dictionary<string, int> _keys = new Dictionary<string, int>();
        private readonly HashSet<string> _keysGivenByNpc = new HashSet<string>();

        public MapData Data { get; }
        public int Width { get; }
        public int Height { get; }
        public GridPos PlayerPos { get; private set; }
        /// <summary>쓴 턴 수 (결과 제출의 moves). 대화와 막힌 입력은 세지 않는다</summary>
        public int Turns { get; private set; }
        public bool IsCleared { get; private set; }

        /// <summary>모든 오브젝트 (IsRemoved 포함). 화면을 처음 그릴 때 쓴다</summary>
        public IReadOnlyList<BoardObject> Objects => _objects;

        public Board(MapData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Width = data.Width;
            Height = data.Height;
            _tiles = data.Tiles.ToArray();
            if (_tiles.Length != Height || _tiles.Any(row => row.Length != Width))
            {
                throw new ArgumentException($"tiles 크기가 {Width}x{Height}와 다릅니다.");
            }
            PlayerPos = new GridPos(data.Player.X, data.Player.Y);
            _objects = data.Objects.Select(o => new BoardObject(o)).ToList();
        }

        public bool IsInside(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public bool IsWall(GridPos p) => !IsInside(p) || _tiles[p.Y][p.X] == '#';

        /// <summary>그 칸의 오브젝트 (사라진 것 제외). 없으면 null</summary>
        public BoardObject ObjectAt(GridPos p) => _objects.FirstOrDefault(o => !o.IsRemoved && o.Pos == p);

        public int KeyCount(string keyId) => keyId != null && _keys.TryGetValue(keyId, out int n) ? n : 0;

        /// <summary>가진 열쇠 전체 (keyId -> 개수)</summary>
        public IReadOnlyDictionary<string, int> Keys => _keys;

        /// <summary>W·A·S·D 입력 하나를 처리한다</summary>
        public MoveResult Move(Direction direction)
        {
            GridPos from = PlayerPos;
            if (IsCleared) return new MoveResult(MoveOutcome.Blocked, direction, from, from, null);

            GridPos to = from + direction.Delta();
            if (IsWall(to)) return new MoveResult(MoveOutcome.Blocked, direction, from, from, null);

            BoardObject target = ObjectAt(to);
            if (target == null) return Step(MoveOutcome.Moved, direction, from, to, null);

            switch (target.Type)
            {
                case MapObjectTypes.Npc:
                    return new MoveResult(MoveOutcome.Blocked, direction, from, from, target);

                case MapObjectTypes.Crate:
                {
                    GridPos beyond = to + direction.Delta();
                    if (IsWall(beyond) || ObjectAt(beyond) != null)
                    {
                        return new MoveResult(MoveOutcome.Blocked, direction, from, from, target);
                    }
                    target.Pos = beyond;
                    Turns++;
                    return new MoveResult(MoveOutcome.Pushed, direction, from, from, target);
                }

                case MapObjectTypes.Breakable:
                    target.Hp--;
                    Turns++;
                    if (target.Hp > 0) return new MoveResult(MoveOutcome.Damaged, direction, from, from, target);
                    target.IsRemoved = true;
                    return new MoveResult(MoveOutcome.Broke, direction, from, from, target);

                case MapObjectTypes.Door:
                    if (KeyCount(target.KeyId) == 0)
                    {
                        return new MoveResult(MoveOutcome.Blocked, direction, from, from, target);
                    }
                    _keys[target.KeyId]--;
                    target.IsRemoved = true;
                    return Step(MoveOutcome.OpenedDoor, direction, from, to, target);

                case MapObjectTypes.Key:
                    target.IsRemoved = true;
                    AddKey(target.KeyId);
                    return Step(MoveOutcome.PickedUpKey, direction, from, to, target);

                case MapObjectTypes.Exit:
                    IsCleared = true;
                    return Step(MoveOutcome.Cleared, direction, from, to, target);

                default:
                    // v1에 없는 type은 바닥처럼 지나간다 (새 버전 맵을 옛 클라이언트가 열어도 멈추지 않게)
                    return Step(MoveOutcome.Moved, direction, from, to, target);
            }
        }

        /// <summary>플레이어 주변 3x3 안의 인물 (가까운 순). 없으면 빈 목록</summary>
        public IReadOnlyList<BoardObject> NpcsInRange() => _objects
            .Where(o => !o.IsRemoved && o.Type == MapObjectTypes.Npc && o.Pos.IsWithin3x3(PlayerPos))
            .OrderBy(o => Math.Abs(o.Pos.X - PlayerPos.X) + Math.Abs(o.Pos.Y - PlayerPos.Y))
            .ToList();

        /// <summary>E를 눌렀을 때 대화할 인물 (3x3 안에서 가장 가까운). 없으면 null</summary>
        public BoardObject TalkableNpc => NpcsInRange().FirstOrDefault();

        /// <summary>대화 effect giveKey. 같은 인물은 같은 열쇠를 한 판에 한 번만 준다</summary>
        /// <returns>이번에 줬으면 true</returns>
        public bool GiveKeyFrom(BoardObject npc, string keyId)
        {
            if (!_keysGivenByNpc.Add(npc.Id + "/" + keyId)) return false;
            AddKey(keyId);
            return true;
        }

        /// <summary>결과 제출 요청. 클리어 전이면 실패(게임 오버·포기)로 보낸다</summary>
        public StageResultRequest ToResultRequest(IEnumerable<long> encounteredGuidebookIds = null) => new StageResultRequest
        {
            Cleared = IsCleared,
            Moves = IsCleared ? Turns : (int?)null,
            HiddenFound = false,
            EncounteredGuidebookIds = encounteredGuidebookIds?.ToList() ?? new List<long>()
        };

        private MoveResult Step(MoveOutcome outcome, Direction direction, GridPos from, GridPos to, BoardObject target)
        {
            PlayerPos = to;
            Turns++;
            return new MoveResult(outcome, direction, from, to, target);
        }

        private void AddKey(string keyId)
        {
            _keys[keyId] = KeyCount(keyId) + 1;
        }
    }
}
