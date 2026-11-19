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
        /// <summary>checkpoint: 구간 번호 (1부터)</summary>
        public int Order { get; }
        /// <summary>checkpoint: 여기서 시작하는 구간의 턴. null이면 스테이지 턴 제한을 쓴다</summary>
        public int? SegmentTurns { get; }
        /// <summary>anomaly: 만나면 해금되는 도감 ID</summary>
        public long? GuidebookId { get; }
        /// <summary>부서진 블록, 주운 열쇠, 열린 문, 모은 숨겨진 요소는 true. 더는 칸을 차지하지 않는다</summary>
        public bool IsRemoved { get; internal set; }
        /// <summary>
        /// 정체가 드러났는지. 숨겨진 요소는 처음에 false이고 3x3 안에 들어오면 true,
        /// 가짜 벽은 처음에 false(벽으로 보임)이고 들어가면 true. 한 번 true가 되면 워프해도 유지한다
        /// </summary>
        public bool IsRevealed { get; internal set; }

        internal BoardObject(MapObjectData data)
        {
            Id = data.Id;
            Type = data.Type;
            Pos = new GridPos(data.X, data.Y);
            Hp = data.Hp ?? 1;
            KeyId = data.KeyId;
            Name = data.Name;
            DialogueId = data.DialogueId;
            Order = data.Order ?? 0;
            SegmentTurns = data.Turns;
            GuidebookId = data.GuidebookId;
            IsRevealed = Type != MapObjectTypes.Hidden && Type != MapObjectTypes.FakeWall;
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
        /// <summary>다음 체크포인트에 도착했다 (Board.LastArrivalStable로 안정·불안정)</summary>
        ReachedCheckpoint,
        /// <summary>숨겨진 요소를 모았다</summary>
        Collected,
        /// <summary>가짜 벽을 처음 지나갔다 (드러남). 이미 드러난 가짜 벽은 Moved</summary>
        FoundFakeWall,
        Cleared
    }

    /// <summary>입력 하나의 결과. 화면은 이 값을 보고 애니메이션·효과음을 정한다</summary>
    public readonly struct MoveResult
    {
        public MoveOutcome Outcome { get; }
        public Direction Direction { get; }
        public GridPos PlayerFrom { get; }
        public GridPos PlayerTo { get; }
        /// <summary>영향을 받은 오브젝트 (상자·블록·열쇠·문·체크포인트·숨겨진 요소·출구·막은 인물·이상 개체). 바닥 이동이나 벽이면 null</summary>
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
    /// 맵은 체크포인트로 구간을 나누고, 구간마다 턴을 센다. 턴을 넘겨도 실패하지 않고 도착이 "불안정"으로 기록된다.
    /// 도착한 체크포인트로는 언제든 워프할 수 있다 (그때 저장한 상태로 복원). 다시 하기는 새 Board를 만든다.
    /// </summary>
    public class Board
    {
        /// <summary>시작 위치의 구간 번호</summary>
        public const int StartOrder = 0;

        /// <summary>안정·불안정 기록에서 출구를 나타내는 번호</summary>
        public const int ExitOrder = int.MaxValue;

        private readonly string[] _tiles;
        private readonly List<BoardObject> _objects;
        private readonly Dictionary<string, int> _keys = new Dictionary<string, int>();
        private readonly HashSet<string> _keysGivenByNpc = new HashSet<string>();

        /// <summary>도착한 체크포인트(시작 위치 = 0)마다 마지막으로 도착했을 때의 상태</summary>
        private readonly SortedDictionary<int, Snapshot> _snapshots = new SortedDictionary<int, Snapshot>();
        /// <summary>체크포인트(출구 = ExitOrder)마다 가장 좋았던 도착이 안정이었는지</summary>
        private readonly Dictionary<int, bool> _stable = new Dictionary<int, bool>();
        /// <summary>구간(시작 체크포인트 번호)마다 통과할 때 쓴 턴의 최솟값</summary>
        private readonly Dictionary<int, int> _segmentBest = new Dictionary<int, int>();
        /// <summary>만난 이상 개체 (오브젝트 id). 워프해도 유지</summary>
        private readonly HashSet<string> _encountered = new HashSet<string>();
        private readonly List<BoardObject> _lastEncounters = new List<BoardObject>();
        /// <summary>미니맵에서 밝혀진 칸 [y * Width + x]. 워프해도 유지</summary>
        private bool[] _explored;

        /// <summary>미니맵 기본 밝힘 범위 (플레이어 둘레 칸 수)</summary>
        public const int DefaultRevealRadius = 2;

        /// <summary>미니맵이 밝혀지는 범위 (map_data minimap.revealRadius, 없거나 1 미만이면 기본값)</summary>
        public int RevealRadius => Data.Minimap?.RevealRadius is int r && r >= 1 ? r : DefaultRevealRadius;

        /// <summary>미니맵에서 밝혀진 칸인지 (플레이어가 RevealRadius 안에 들어간 적 있는 칸)</summary>
        public bool IsExplored(GridPos p) => IsInside(p) && _explored[p.Y * Width + p.X];

        public MapData Data { get; }
        public int Width { get; }
        public int Height { get; }
        public GridPos PlayerPos { get; private set; }

        /// <summary>누른 턴 전체 (워프와 상관없이 누적, 통계용). 대화와 막힌 입력은 세지 않는다</summary>
        public int Turns { get; private set; }

        /// <summary>지금 구간에서 쓴 턴. 체크포인트에 도착하거나 워프하면 0부터 다시 센다</summary>
        public int SegmentTurns { get; private set; }

        /// <summary>지금 구간이 시작한 체크포인트 번호 (시작 위치 = 0)</summary>
        public int CurrentOrder { get; private set; } = StartOrder;

        public bool IsCleared { get; private set; }

        /// <summary>워프한 횟수</summary>
        public int Rewinds { get; private set; }

        /// <summary>방금 도착한 체크포인트·출구가 안정이었는지 (ReachedCheckpoint, Cleared 결과 직후에 본다)</summary>
        public bool LastArrivalStable { get; private set; }

        /// <summary>턴 제한 (스테이지 parMoves). 시작 구간의 턴이다. null이면 제한 없음</summary>
        public int? TurnLimit { get; }

        /// <summary>지금 구간의 턴. 시작 구간은 TurnLimit, 체크포인트 구간은 그 체크포인트의 turns. null이면 제한 없음</summary>
        public int? SegmentLimit { get; private set; }

        /// <summary>남은 턴 = 구간 턴 - 구간에서 쓴 턴. 음수면 초과 (실패는 아님). 제한이 없으면 null</summary>
        public int? RemainingTurns => SegmentLimit - SegmentTurns;

        /// <summary>지금 구간의 턴을 넘겼는지 (이대로 도착하면 불안정)</summary>
        public bool IsOverLimit => RemainingTurns < 0;

        /// <summary>클리어해서 더는 입력을 받지 않는지</summary>
        public bool IsOver => IsCleared;

        /// <summary>모든 오브젝트 (IsRemoved 포함). 화면을 처음 그릴 때 쓴다</summary>
        public IReadOnlyList<BoardObject> Objects => _objects;

        /// <summary>워프할 수 있는 체크포인트 번호 (시작 위치 0 포함, 오름차순)</summary>
        public IReadOnlyList<int> WarpPoints => _snapshots.Keys.ToList();

        /// <summary>맵의 체크포인트 수</summary>
        public int CheckpointCount => _objects.Count(o => o.Type == MapObjectTypes.Checkpoint);

        /// <summary>불안정 체크포인트(출구 포함) 수. 별 2 판정에 쓴다</summary>
        public int UnstableCount => _stable.Values.Count(stable => !stable);

        /// <summary>그 체크포인트(출구 = ExitOrder)에 도착한 적 있으면 안정 여부, 없으면 null</summary>
        public bool? StabilityOf(int order) => _stable.TryGetValue(order, out bool s) ? s : (bool?)null;

        /// <summary>기록 = 구간 최고 기록의 합 (결과 제출의 moves). 탐색하다 워프로 돌아가 다시 통과하면 좋은 쪽만 남는다</summary>
        public int RecordMoves => _segmentBest.Values.Sum();

        /// <summary>별 2 조건을 만족하는지 (불안정 수 &lt;= 맵의 stars.maxUnstable)</summary>
        public bool MeetsStableStar => UnstableCount <= Math.Max(0, Data.Stars?.MaxUnstable ?? 0);

        /// <summary>맵의 숨겨진 요소 수</summary>
        public int HiddenTotal => _objects.Count(o => o.Type == MapObjectTypes.Hidden);

        /// <summary>모은 숨겨진 요소 수 (워프해도 유지)</summary>
        public int HiddenCollected => _objects.Count(o => o.Type == MapObjectTypes.Hidden && o.IsRemoved);

        /// <summary>숨겨진 요소를 모두 모았는지 (별 3). 맵에 없으면 false</summary>
        public bool AllHiddenFound => HiddenTotal > 0 && HiddenCollected == HiddenTotal;

        /// <summary>만난 이상 개체의 도감 ID (결과 제출의 encounteredGuidebookIds, 워프해도 유지)</summary>
        public IReadOnlyCollection<long> EncounteredGuidebookIds => _objects
            .Where(o => o.Type == MapObjectTypes.Anomaly && o.GuidebookId != null && _encountered.Contains(o.Id))
            .Select(o => o.GuidebookId.Value).Distinct().ToList();

        /// <summary>방금(시작·이동·워프) 처음 만난 이상 개체. 화면 알림용</summary>
        public IReadOnlyList<BoardObject> LastEncounters => _lastEncounters;

        /// <param name="turnLimit">턴 제한 (스테이지 parMoves). null이면 제한 없음</param>
        public Board(MapData data, int? turnLimit = null)
        {
            TurnLimit = turnLimit;
            SegmentLimit = turnLimit;
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
            _explored = new bool[Width * Height];
            _snapshots[StartOrder] = Capture();
            LookAround();
        }

        public bool IsInside(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public bool IsWall(GridPos p) => !IsInside(p) || _tiles[p.Y][p.X] == '#';

        /// <summary>그 칸의 오브젝트 (사라진 것 제외). 없으면 null</summary>
        public BoardObject ObjectAt(GridPos p) => _objects.FirstOrDefault(o => !o.IsRemoved && o.Pos == p);

        public int KeyCount(string keyId) => keyId != null && _keys.TryGetValue(keyId, out int n) ? n : 0;

        /// <summary>가진 열쇠 전체 (keyId -> 개수)</summary>
        public IReadOnlyDictionary<string, int> Keys => _keys;

        /// <summary>그 번호의 체크포인트 오브젝트. 시작 위치(0)나 없는 번호면 null</summary>
        public BoardObject CheckpointOf(int order) =>
            _objects.FirstOrDefault(o => o.Type == MapObjectTypes.Checkpoint && o.Order == order);

        /// <summary>W·A·S·D 입력 하나를 처리한다. 체크포인트·출구에 들어가면 그 구간을 판정한다</summary>
        public MoveResult Move(Direction direction)
        {
            MoveResult result = MoveCore(direction);
            if (result.Outcome == MoveOutcome.ReachedCheckpoint) Arrive(result.Target.Order, result.Target);
            else if (result.Outcome == MoveOutcome.Cleared) Arrive(ExitOrder, null);
            LookAround();
            return result;
        }

        /// <summary>
        /// 도착한 적 있는 체크포인트(시작 위치 0 포함)로 워프한다. 그 체크포인트에 마지막으로 도착했을 때의 상태로 돌아가고
        /// 그 구간을 처음부터 시작한다. 턴을 쓰지 않는다. 안정 기록과 구간 최고 기록은 유지된다.
        /// </summary>
        /// <returns>워프했으면 true (클리어 후거나 도착하지 않은 체크포인트면 false)</returns>
        public bool WarpTo(int order)
        {
            if (IsOver || !_snapshots.TryGetValue(order, out Snapshot snapshot)) return false;
            Restore(snapshot);
            StartSegment(order);
            Rewinds++;
            LookAround();
            return true;
        }

        private MoveResult MoveCore(Direction direction)
        {
            GridPos from = PlayerPos;
            if (IsOver) return new MoveResult(MoveOutcome.Blocked, direction, from, from, null);

            GridPos to = from + direction.Delta();
            if (IsWall(to)) return new MoveResult(MoveOutcome.Blocked, direction, from, from, null);

            BoardObject target = ObjectAt(to);
            if (target == null) return Step(MoveOutcome.Moved, direction, from, to, null);

            switch (target.Type)
            {
                case MapObjectTypes.Npc:
                case MapObjectTypes.Anomaly:
                    return new MoveResult(MoveOutcome.Blocked, direction, from, from, target);

                case MapObjectTypes.Hidden:
                    target.IsRemoved = true;
                    target.IsRevealed = true;
                    return Step(MoveOutcome.Collected, direction, from, to, target);

                case MapObjectTypes.FakeWall:
                {
                    bool first = !target.IsRevealed;
                    target.IsRevealed = true;
                    return Step(first ? MoveOutcome.FoundFakeWall : MoveOutcome.Moved, direction, from, to, target);
                }

                case MapObjectTypes.Crate:
                {
                    GridPos beyond = to + direction.Delta();
                    // 가짜 벽도 오브젝트라 상자는 밀어 넣을 수 없다 (벽처럼 보이므로)
                    if (IsWall(beyond) || ObjectAt(beyond) != null)
                    {
                        return new MoveResult(MoveOutcome.Blocked, direction, from, from, target);
                    }
                    target.Pos = beyond;
                    UseTurn();
                    return new MoveResult(MoveOutcome.Pushed, direction, from, from, target);
                }

                case MapObjectTypes.Breakable:
                    target.Hp--;
                    UseTurn();
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

                case MapObjectTypes.Checkpoint:
                    // 앞쪽(번호가 큰) 체크포인트에 들어갈 때만 도착이다. 지나온 체크포인트는 바닥처럼 지나간다 (돌아가려면 워프)
                    return Step(target.Order > CurrentOrder ? MoveOutcome.ReachedCheckpoint : MoveOutcome.Moved,
                        direction, from, to, target);

                case MapObjectTypes.Exit:
                    IsCleared = true;
                    return Step(MoveOutcome.Cleared, direction, from, to, target);

                default:
                    // v1에 없는 type은 바닥처럼 지나간다 (새 버전 맵을 옛 클라이언트가 열어도 멈추지 않게)
                    return Step(MoveOutcome.Moved, direction, from, to, target);
            }
        }

        /// <summary>지금 구간을 끝낸다: 안정 판정, 구간 최고 기록, (체크포인트면) 상태 저장 후 다음 구간 시작</summary>
        private void Arrive(int order, BoardObject checkpoint)
        {
            bool stable = !IsOverLimit;
            LastArrivalStable = stable;
            _stable[order] = (_stable.TryGetValue(order, out bool before) && before) || stable;
            _segmentBest[CurrentOrder] = _segmentBest.TryGetValue(CurrentOrder, out int best)
                ? Math.Min(best, SegmentTurns)
                : SegmentTurns;
            if (checkpoint == null) return; // 출구: 클리어

            StartSegment(order);
            _snapshots[order] = Capture();
        }

        private void StartSegment(int order)
        {
            CurrentOrder = order;
            SegmentTurns = 0;
            SegmentLimit = order == StartOrder ? TurnLimit : (CheckpointOf(order)?.SegmentTurns ?? TurnLimit);
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

        /// <summary>결과 제출 요청. 클리어하지 못했으면(포기 포함) 실패로 보낸다</summary>
        public StageResultRequest ToResultRequest(IEnumerable<long> encounteredGuidebookIds = null) => new StageResultRequest
        {
            Cleared = IsCleared,
            Moves = IsCleared ? Math.Max(1, RecordMoves) : (int?)null,
            HiddenFound = IsCleared && AllHiddenFound,
            EncounteredGuidebookIds = EncounteredGuidebookIds.Concat(encounteredGuidebookIds ?? Enumerable.Empty<long>())
                .Distinct().ToList(),
            UnstableCount = UnstableCount,
            Rewinds = Rewinds
        };

        private MoveResult Step(MoveOutcome outcome, Direction direction, GridPos from, GridPos to, BoardObject target)
        {
            PlayerPos = to;
            UseTurn();
            return new MoveResult(outcome, direction, from, to, target);
        }

        /// <summary>플레이어 주변 3x3: 숨겨진 요소를 드러내고, 처음 만난 이상 개체를 기록한다. 미니맵도 RevealRadius만큼 밝힌다</summary>
        private void LookAround()
        {
            int radius = RevealRadius;
            for (int y = Math.Max(0, PlayerPos.Y - radius); y <= Math.Min(Height - 1, PlayerPos.Y + radius); y++)
            for (int x = Math.Max(0, PlayerPos.X - radius); x <= Math.Min(Width - 1, PlayerPos.X + radius); x++)
            {
                _explored[y * Width + x] = true;
            }

            _lastEncounters.Clear();
            foreach (BoardObject o in _objects)
            {
                if (o.IsRemoved || !o.Pos.IsWithin3x3(PlayerPos)) continue;
                if (o.Type == MapObjectTypes.Hidden) o.IsRevealed = true;
                else if (o.Type == MapObjectTypes.Anomaly && _encountered.Add(o.Id)) _lastEncounters.Add(o);
            }
        }

        private void UseTurn()
        {
            Turns++;
            SegmentTurns++;
        }

        private void AddKey(string keyId)
        {
            _keys[keyId] = KeyCount(keyId) + 1;
        }

        // ---------- 워프용 상태 저장 ----------

        /// <summary>워프로 되돌릴 상태: 위치, 오브젝트(위치·hp·사라짐, 숨겨진 요소 제외), 열쇠, 대화로 받은 열쇠 기록</summary>
        private sealed class Snapshot
        {
            public GridPos Player;
            public (GridPos Pos, int Hp, bool Removed)[] Objects;
            public Dictionary<string, int> Keys;
            public HashSet<string> KeysGivenByNpc;
        }

        private Snapshot Capture() => new Snapshot
        {
            Player = PlayerPos,
            Objects = _objects.Select(o => (o.Pos, o.Hp, o.IsRemoved)).ToArray(),
            Keys = new Dictionary<string, int>(_keys),
            KeysGivenByNpc = new HashSet<string>(_keysGivenByNpc)
        };

        private void Restore(Snapshot s)
        {
            PlayerPos = s.Player;
            for (int i = 0; i < _objects.Count; i++)
            {
                // 숨겨진 요소의 수집·드러남은 워프해도 유지한다
                if (_objects[i].Type == MapObjectTypes.Hidden) continue;
                _objects[i].Pos = s.Objects[i].Pos;
                _objects[i].Hp = s.Objects[i].Hp;
                _objects[i].IsRemoved = s.Objects[i].Removed;
            }
            _keys.Clear();
            foreach (var kv in s.Keys) _keys[kv.Key] = kv.Value;
            _keysGivenByNpc.Clear();
            _keysGivenByNpc.UnionWith(s.KeysGivenByNpc);
        }
    }
}
