using System.Collections.Generic;
using System.Linq;
using Executor.Data;
using Executor.Game;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// Board를 화면에 그린다. 한 칸 = 1 유닛 (도트 16px, Pixels Per Unit 16).
    /// map_data는 y가 아래로 커지고 Unity는 위로 커지므로 worldY = height - 1 - y.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        /// <summary>한 칸 움직이는 데 걸리는 시간 (걷는 동작이 보이게)</summary>
        private const float MoveSeconds = 0.18f;
        /// <summary>걷기 프레임 하나의 시간. 한 칸에 프레임 1~2개가 지나간다 (GIF 원본은 0.15초)</summary>
        private const float WalkFrameSeconds = 0.12f;

        /// <summary>구간 화면: 구간 시작·끝과 플레이어 둘레로 남기는 칸 수</summary>
        private const int ViewPadding = 2;
        /// <summary>구간 화면의 최소 크기 (칸). 구간이 짧아도 이보다 크게 확대하지 않는다</summary>
        private const int MinViewWidth = 9, MinViewHeight = 7;
        /// <summary>카메라가 목표를 따라가는 빠르기 (클수록 빠름)</summary>
        private const float CameraSharpness = 8f;

        // 카메라 목표 (Update에서 부드럽게 따라간다)
        private Vector3 _camPos;
        private float _camSize;
        private bool _hasCamTarget;

        /// <summary>이 판의 구간 보기 크기 (Build 때 한 번 정해 판 내내 유지)</summary>
        private RectInt _segmentView;

        /// <summary>true면 맵 전체를 보여 준다 (M 키). false면 지금 구간을 확대해 플레이어를 따라간다</summary>
        public bool Overview { get; private set; }

        private Board _board;
        private Transform _player;
        private SpriteRenderer _playerSprite;
        /// <summary>플레이어가 보는 방향 (마지막으로 누른 방향)</summary>
        private Direction _facing = Direction.Down;
        /// <summary>걷는 동작을 더 보여 줄 시간. 칸을 옮길 때마다 채운다</summary>
        private float _walkLeft;
        /// <summary>이번 걸음이 시작된 뒤 지난 시간</summary>
        private float _stepClock;
        /// <summary>
        /// 지금까지 걸은 걸음 수. 걸음마다 시작 프레임을 번갈아 정해(오른발 → 왼발 → ...) 멈췄다 걸어도 같은 발만 나가지 않게 한다
        /// </summary>
        private int _steps;
        private readonly Dictionary<BoardObject, SpriteRenderer> _objects = new Dictionary<BoardObject, SpriteRenderer>();
        /// <summary>맵 도트 세트(Resources/Pixel/map)로 그리는지</summary>
        private bool _mapSet;

        /// <summary>맵 세트의 타일 층: 바닥 → 기판 두께 → 벽 → 벽 장식. 오브젝트·플레이어는 그 위(SortOrder)</summary>
        private const int LayerFloor = 0, LayerEdge = 1, LayerWall = 2, LayerDeco = 3;
        /// <summary>판 테두리 선 (발광 없이 밝은 남색, 시안 README)</summary>
        private static readonly Color BoardLine = new Color32(0x4c, 0x4a, 0xa8, 0xff);
        private readonly Dictionary<Transform, Vector3> _targets = new Dictionary<Transform, Vector3>();
        private Material _material;

        public void Build(Board board)
        {
            Clear();
            _board = board;
            // 2D 라이트가 없는 씬에서도 보이도록 Unlit 머티리얼을 쓴다
            if (_material == null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (unlit != null) _material = new Material(unlit);
            }

            _mapSet = ArtCatalog.HasMapSet;
            if (_mapSet) BuildMapSetTiles(board);
            else
            {
                for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    Sprite tile = board.IsWall(pos) ? WallSprite(board, pos) : ArtCatalog.Floor;
                    // 위쪽 벽 그림은 세로로 길면 윗칸까지 솟으므로 다른 타일보다 위에
                    Spawn(board.IsWall(pos) ? "wall" : "floor", tile, pos, tile != null && tile == ArtCatalog.WallTop ? 1 : 0);
                }
            }
            foreach (BoardObject obj in board.Objects)
            {
                string characterId = obj.Type == MapObjectTypes.Npc ? CharacterCatalog.ByName(obj.Name)?.CharacterKey : null;
                SpriteRenderer sr = Spawn(obj.Id, ArtCatalog.ForObject(obj.Type, characterId), obj.Pos, SortOrder(obj.Pos.Y, false));
                _objects[obj] = sr;
                // 가짜 벽도 진짜 벽과 같은 장식을 단다 (장식이 없어서 들키지 않게)
                if (_mapSet && obj.Type == MapObjectTypes.FakeWall)
                {
                    Sprite deco = DecoSprite(board, obj.Pos);
                    if (deco != null)
                    {
                        SpriteRenderer d = Spawn(obj.Id + ":deco", deco, obj.Pos, LayerDeco);
                        d.transform.SetParent(sr.transform, true);
                    }
                }
            }
            _facing = Direction.Down;
            _walkLeft = 0;
            _steps = 0;
            _playerSprite = Spawn("player", ArtCatalog.PlayerFrames(_facing)[0], board.PlayerPos, SortOrder(board.PlayerPos.Y, true));
            _player = _playerSprite.transform;
            Overview = false;
            _segmentView = default;
            Sync();
            SnapAll();
            UpdateCamera(true);
        }

        /// <summary>Board 상태에 맞춰 위치·표시를 갱신한다 (이동은 짧게 미끄러지듯)</summary>
        public void Sync()
        {
            if (_board == null) return;
            Vector3 playerTarget = ToWorld(_board.PlayerPos);
            // 다른 칸으로 옮기면 그동안 걷는 동작을 보여 준다
            if (_targets.TryGetValue(_player, out Vector3 before) && before != playerTarget)
            {
                // 새 걸음: 걸음 수를 늘리고 이번 걸음의 시간을 처음부터
                _steps++;
                _stepClock = 0;
                _walkLeft = MoveSeconds;
            }
            _targets[_player] = playerTarget;
            _playerSprite.sortingOrder = SortOrder(_board.PlayerPos.Y, true);
            foreach (var pair in _objects)
            {
                BoardObject obj = pair.Key;
                SpriteRenderer sr = pair.Value;
                if (obj.Type == MapObjectTypes.FakeWall)
                {
                    // 가짜 벽: 드러난 뒤에도 화면에서는 계속 진짜 벽과 똑같이 그린다 (드러남은 미니맵에만 표시)
                    sr.gameObject.SetActive(!obj.IsRemoved);
                    sr.sprite = WallSprite(_board, obj.Pos);
                    if (_mapSet)
                    {
                        sr.sortingOrder = LayerWall; // 벽과 같은 층 (오브젝트·플레이어가 위로 지나간다)
                        continue;
                    }
                }
                else if (_mapSet && obj.Type == MapObjectTypes.Door && ArtCatalog.DoorOpen != null)
                {
                    // 열린 문은 사라지지 않고 열린 셔터로 바닥 층에 남는다
                    sr.gameObject.SetActive(obj.IsRevealed);
                    sr.sprite = obj.IsRemoved ? ArtCatalog.DoorOpen : ArtCatalog.ForObject(obj.Type);
                    _targets[sr.transform] = ToWorld(obj.Pos);
                    sr.sortingOrder = obj.IsRemoved ? LayerDeco : SortOrder(obj.Pos.Y, false);
                    continue;
                }
                else sr.gameObject.SetActive(!obj.IsRemoved && obj.IsRevealed);
                // 체크포인트: 도착했으면 켜진 그림
                if (_mapSet && obj.Type == MapObjectTypes.Checkpoint && ArtCatalog.CheckpointOn != null)
                {
                    sr.sprite = _board.StabilityOf(obj.Order) != null ? ArtCatalog.CheckpointOn : ArtCatalog.ForObject(obj.Type);
                }
                // 컴퓨터: 감염 / 복구
                if (obj.Type == MapObjectTypes.Computer)
                {
                    sr.sprite = obj.IsRestored ? ArtCatalog.ComputerRestored : ArtCatalog.ForObject(obj.Type);
                }
                _targets[sr.transform] = ToWorld(obj.Pos);
                sr.sortingOrder = SortOrder(obj.Pos.Y, false); // 상자는 밀려서 줄이 바뀔 수 있다
                // 방화벽: 단계 그림이 있으면 남은 내구도에 맞는 그림, 없으면 한 장을 남은 내구도만큼 어둡게
                if (obj.Type == MapObjectTypes.Breakable)
                {
                    int max = _board.Data.Objects.Find(o => o.Id == obj.Id)?.Hp ?? 1;
                    Sprite[] stages = ArtCatalog.BreakableStages;
                    if (_mapSet && ArtCatalog.FirewallDamaged != null)
                    {
                        // 맵 세트: 한 번이라도 맞으면 금 간 방화벽
                        sr.sprite = obj.Hp < max ? ArtCatalog.FirewallDamaged : ArtCatalog.ForObject(obj.Type);
                        sr.color = Color.white;
                    }
                    else if (stages.Length > 0)
                    {
                        sr.sprite = stages[ArtCatalog.BreakableStage(obj.Hp, max, stages.Length)];
                        sr.color = Color.white;
                    }
                    else
                    {
                        float t = max <= 1 ? 1f : 0.45f + 0.55f * obj.Hp / max;
                        sr.color = new Color(t, t, t, 1f);
                    }
                }
            }
            UpdateCamera(false);
        }

        /// <summary>
        /// 그리는 순서: 바닥·벽(0) 위에, 아랫줄일수록 앞에. 윗칸까지 솟는 그림(컴퓨터, 플레이어 머리)이
        /// 아랫줄의 것을 가리지 않게 한다. 같은 줄이면 플레이어가 오브젝트 앞
        /// </summary>
        private static int SortOrder(int row, bool player) => 10 + row * 2 + (player ? 1 : 0);

        /// <summary>화면에서 벽으로 보이는 칸: 진짜 벽 + 가짜 벽 (가짜 벽은 드러난 뒤에도 화면에서는 벽)</summary>
        private static bool LooksLikeWall(Board board, GridPos p) =>
            board.IsWall(p) || board.Objects.Any(o => o.Type == MapObjectTypes.FakeWall && !o.IsRemoved && o.Pos.Equals(p));

        /// <summary>벽처럼 이어 그리는 칸: 화면에서 벽인 칸 + 맵 바깥 (테두리 벽이 바깥으로 이어져 보이게)</summary>
        private static bool WallLike(Board board, GridPos p) => !board.IsInside(p) || LooksLikeWall(board, p);

        /// <summary>
        /// 맵 세트에서 그리는 벽인지. 맵 가장자리 벽은 아래가 바닥인 윗벽만 그리고, 옆·아래 테두리는 비워 둔다 (시안 README)
        /// </summary>
        private static bool DrawnWall(Board board, GridPos p)
        {
            if (!board.IsWall(p)) return false;
            bool border = p.X == 0 || p.Y == 0 || p.X == board.Width - 1 || p.Y == board.Height - 1;
            if (!border) return true;
            var below = new GridPos(p.X, p.Y + 1);
            return board.IsInside(below) && !LooksLikeWall(board, below);
        }

        /// <summary>
        /// 맵 세트 타일: 바닥(무늬 섞기), 벽 자동 연결(wall_NESW) + 장식, 그린 칸 바로 아래가 빈 공간이면 기판 두께, 판 테두리 선
        /// </summary>
        private void BuildMapSetTiles(Board board)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var pos = new GridPos(x, y);
                bool drawn;
                if (board.IsWall(pos))
                {
                    drawn = DrawnWall(board, pos);
                    if (drawn)
                    {
                        Spawn("wall", WallSprite(board, pos), pos, LayerWall);
                        Sprite deco = DecoSprite(board, pos);
                        if (deco != null) Spawn("deco", deco, pos, LayerDeco);
                    }
                }
                else
                {
                    drawn = true;
                    Spawn("floor", ArtCatalog.FloorTile(x, y), pos, LayerFloor);
                }
                if (drawn)
                {
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
                var below = new GridPos(x, y + 1);
                if (drawn && ArtCatalog.BoardEdge != null && (!board.IsInside(below) || (board.IsWall(below) && !DrawnWall(board, below))))
                {
                    Spawn("edge", ArtCatalog.BoardEdge, below, LayerEdge);
                }
            }
            if (minX <= maxX)
            {
                // 그린 칸(바닥·벽)을 감싸고, 아래는 기판 두께(board_edge 위쪽 18px)까지
                float edge = ArtCatalog.BoardEdge != null ? 18f / ArtCatalog.ArtPixelsPerTile : 0;
                float h = board.Height;
                SpawnBoardFrame(minX - 0.5f, maxX + 0.5f, h - 1 - minY + 0.5f, h - 1 - maxY - 0.5f - edge);
            }
        }

        /// <summary>벽 장식 (가짜 벽도 같은 규칙)</summary>
        private static Sprite DecoSprite(Board board, GridPos p)
        {
            var below = new GridPos(p.X, p.Y + 1);
            bool front = board.IsInside(below) && !LooksLikeWall(board, below);
            return ArtCatalog.WallDeco(p.X, p.Y, WallLike(board, new GridPos(p.X + 1, p.Y)), front, WallLike(board, new GridPos(p.X - 1, p.Y)));
        }

        private static Sprite _pixel;

        /// <summary>판 둘레에 남색 선 (월드 좌표 왼쪽·오른쪽·위·아래 바깥쪽에 두께 2px, 칸 1개 = 64px 기준)</summary>
        private void SpawnBoardFrame(float left, float right, float top, float bottom)
        {
            if (_pixel == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            }
            const float t = 2f / ArtCatalog.ArtPixelsPerTile;
            float w = right - left, h = top - bottom, cx = (left + right) / 2f, cy = (top + bottom) / 2f;
            var lines = new (Vector3 pos, Vector3 size)[]
            {
                (new Vector3(cx, top + t / 2, 0), new Vector3(w + 2 * t, t, 1)),
                (new Vector3(cx, bottom - t / 2, 0), new Vector3(w + 2 * t, t, 1)),
                (new Vector3(left - t / 2, cy, 0), new Vector3(t, h, 1)),
                (new Vector3(right + t / 2, cy, 0), new Vector3(t, h, 1)),
            };
            foreach (var (pos, size) in lines)
            {
                var go = new GameObject("frame");
                go.transform.SetParent(transform, false);
                go.transform.position = pos;
                go.transform.localScale = size;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _pixel;
                sr.color = BoardLine;
                sr.sortingOrder = LayerDeco;
                if (_material != null) sr.sharedMaterial = _material;
            }
        }

        /// <summary>벽 그림. 맵 세트면 이웃 벽에 맞춰 자동 연결, 아니면 바로 아래 칸이 바닥일 때(위쪽 벽 한 줄) wall_top 그림, 그 밖에 보통 벽</summary>
        private static Sprite WallSprite(Board board, GridPos p)
        {
            if (ArtCatalog.HasMapSet)
            {
                Sprite tile = ArtCatalog.WallTile(WallLike(board, new GridPos(p.X, p.Y - 1)), WallLike(board, new GridPos(p.X + 1, p.Y)),
                    WallLike(board, new GridPos(p.X, p.Y + 1)), WallLike(board, new GridPos(p.X - 1, p.Y)));
                if (tile != null) return tile;
            }
            Sprite top = ArtCatalog.WallTop;
            if (top == null) return ArtCatalog.Wall;
            var below = new GridPos(p.X, p.Y + 1);
            return board.IsInside(below) && !LooksLikeWall(board, below) ? top : ArtCatalog.Wall;
        }

        /// <summary>워프: 미끄러지지 않고 바로 그 상태로 옮긴다 (카메라도)</summary>
        public void SnapToBoard()
        {
            Sync();
            _walkLeft = 0; // 워프는 걷지 않는다
            foreach (var pair in _targets)
            {
                if (pair.Key != null) pair.Key.position = pair.Value;
            }
            UpdateCamera(true);
        }

        /// <summary>맵 전체 보기 ↔ 구간 확대를 바꾼다 (M 키)</summary>
        public void ToggleOverview()
        {
            Overview = !Overview;
            UpdateCamera(false);
        }

        /// <summary>플레이어가 그 방향을 보게 한다 (이동·밀기·막힘 모두)</summary>
        public void Face(Direction direction)
        {
            _facing = direction;
        }

        /// <summary>막힌 입력: 그 방향을 보고 살짝 흔들기</summary>
        public void Bump(Direction direction)
        {
            Face(direction);
            GridPos d = direction.Delta();
            _player.position = ToWorld(_board.PlayerPos) + new Vector3(d.X, -d.Y, 0) * 0.15f;
        }

        public Vector3 ToWorld(GridPos p) => new Vector3(p.X, _board.Height - 1 - p.Y, 0);

        private void Update()
        {
            float step = Time.deltaTime / MoveSeconds;
            foreach (var pair in _targets)
            {
                if (pair.Key != null) pair.Key.position = Vector3.MoveTowards(pair.Key.position, pair.Value, step);
            }
            AnimatePlayer();

            Camera cam = Camera.main;
            if (_hasCamTarget && cam != null)
            {
                float t = 1f - Mathf.Exp(-CameraSharpness * Time.deltaTime);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _camSize, t);
                cam.transform.position = Vector3.Lerp(cam.transform.position, _camPos, t);
            }
        }

        /// <summary>
        /// 플레이어 그림: 보는 방향의 프레임. 걷는 동안(칸을 옮기는 중) 프레임을 넘기고, 멈추면 0번(서 있는 자세).
        /// 계속 눌러 이어 걸으면 걷기 동작도 끊기지 않고 이어진다
        /// </summary>
        private void AnimatePlayer()
        {
            if (_playerSprite == null) return;
            Sprite[] frames = ArtCatalog.PlayerFrames(_facing);
            bool moving = _targets.TryGetValue(_player, out Vector3 target) && (_player.position - target).sqrMagnitude > 0.0001f;
            int index = 0;
            if (frames.Length > 1 && (moving || _walkLeft > 0))
            {
                _walkLeft -= Time.deltaTime;
                _stepClock += Time.deltaTime;
                // 걷기 프레임 한 바퀴 = 두 걸음 (4프레임이면 선 자세·오른발·선 자세·왼발).
                // 한 걸음에 프레임 절반을 쓰고, 걸음 수에 따라 시작 프레임을 번갈아 고른다:
                //   짝수 걸음 1 -> 2, 홀수 걸음 3 -> 0 (= (1 + 걸음 수 × 2) % 4)
                int perStep = Mathf.Max(1, frames.Length / 2);
                int start = 1 + (_steps - 1) * perStep;
                int within = Mathf.Min(perStep - 1, (int)(_stepClock / WalkFrameSeconds));
                index = ((start + within) % frames.Length + frames.Length) % frames.Length;
            }
            else
            {
                _walkLeft = 0;
            }
            if (_playerSprite.sprite != frames[index]) _playerSprite.sprite = frames[index];
        }

        private void SnapAll()
        {
            foreach (var pair in _targets) pair.Key.position = pair.Value;
        }

        private SpriteRenderer Spawn(string name, Sprite sprite, GridPos pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = ToWorld(pos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (_material != null) sr.sharedMaterial = _material;
            return sr;
        }

        /// <summary>
        /// 카메라 목표를 정한다. 맵이 작거나 전체 보기면 맵 전체(+여백 반 칸)를 GameUI.GameArea 안에 맞추고,
        /// 아니면 구간 보기 크기(가장 넓은 구간이 들어가는 크기, 판 내내 같음)로 확대하고 플레이어를 따라간다.
        /// 맵 바깥이 보이지 않게 가장자리에서 멈춘다.
        /// </summary>
        /// <param name="snap">true면 바로 옮긴다 (처음 그릴 때, 워프)</param>
        private void UpdateCamera(bool snap)
        {
            Camera cam = Camera.main;
            if (cam == null || _board == null) return;
            cam.orthographic = true;
            cam.backgroundColor = ArtCatalog.MapBackground;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Rect area = GameUI.GameArea;
            float aspect = cam.aspect;

            // 보여 줄 범위 (칸)
            RectInt view = Overview ? new RectInt(0, 0, _board.Width, _board.Height) : SegmentView();
            float size = Mathf.Max((view.height + 1f) / (2f * area.height), (view.width + 1f) / (2f * aspect * area.width));

            // 화면에 보이는 크기 (칸)
            float visibleW = 2f * size * aspect * area.width;
            float visibleH = 2f * size * area.height;

            // 가운데: 전체 보기면 맵 가운데, 아니면 플레이어. 맵 바깥(반 칸 넘게)이 보이지 않게 가장자리에서 멈춘다
            Vector3 focus = Overview ? new Vector3((_board.Width - 1) / 2f, (_board.Height - 1) / 2f) : ToWorld(_board.PlayerPos);
            float cx = ClampAxis(focus.x, visibleW, _board.Width);
            float cy = ClampAxis(focus.y, visibleH, _board.Height);

            // GameArea가 화면 가운데가 아니므로(HUD 자리) 그만큼 카메라를 옮긴다
            var offset = new Vector2((area.center.x - 0.5f) * 2f * size * aspect, (area.center.y - 0.5f) * 2f * size);
            _camPos = new Vector3(cx - offset.x, cy - offset.y, -10f);
            _camSize = size;
            _hasCamTarget = true;
            if (snap)
            {
                cam.orthographicSize = _camSize;
                cam.transform.position = _camPos;
            }
        }

        /// <summary>한 축의 카메라 중심. 보이는 길이가 맵보다 길면 맵 가운데, 아니면 맵 안(-0.5 ~ length-0.5)에 머물게</summary>
        private static float ClampAxis(float focus, float visible, int length)
        {
            float min = -0.5f + visible / 2f;
            float max = length - 0.5f - visible / 2f;
            return min > max ? (length - 1) / 2f : Mathf.Clamp(focus, min, max);
        }

        /// <summary>
        /// 구간 보기의 크기 (칸). map_data camera.width·height가 있으면 그 값, 없으면 판 내내 같은 크기를 쓰도록 모든 구간(시작 → 체크포인트 1 → … → 출구) 중
        /// 가장 넓은 구간이 들어가는 크기로 한 번 정한다. 여백 포함, 최소 크기 이상, 맵 크기 이하
        /// </summary>
        private RectInt SegmentView()
        {
            if (_segmentView.width > 0) return _segmentView;

            // 맵에 camera 크기가 있으면 그대로 쓴다 (비운 쪽은 1로 두어 다른 쪽이 크기를 정하게)
            CameraSettings fixedView = _board.Data.Camera;
            if (fixedView != null && (fixedView.Width > 0 || fixedView.Height > 0))
            {
                int fw = fixedView.Width > 0 ? fixedView.Width.Value : 1;
                int fh = fixedView.Height > 0 ? fixedView.Height.Value : 1;
                _segmentView = new RectInt(0, 0, Mathf.Min(_board.Width, fw), Mathf.Min(_board.Height, fh));
                return _segmentView;
            }

            // 구간 끝점: 시작 위치, 체크포인트(번호 순), 출구
            var stops = new List<GridPos> { new GridPos(_board.Data.Player.X, _board.Data.Player.Y) };
            for (int order = 1; ; order++)
            {
                BoardObject cp = _board.CheckpointOf(order);
                if (cp == null) break;
                stops.Add(cp.Pos);
            }
            foreach (BoardObject o in _board.Objects)
            {
                if (o.Type == MapObjectTypes.Exit) { stops.Add(o.Pos); break; }
            }

            int w = MinViewWidth, h = MinViewHeight;
            for (int i = 0; i + 1 < stops.Count; i++)
            {
                w = Mathf.Max(w, Mathf.Abs(stops[i + 1].X - stops[i].X) + 1 + ViewPadding * 2);
                h = Mathf.Max(h, Mathf.Abs(stops[i + 1].Y - stops[i].Y) + 1 + ViewPadding * 2);
            }
            _segmentView = new RectInt(0, 0, Mathf.Min(_board.Width, w), Mathf.Min(_board.Height, h));
            return _segmentView;
        }

        private void Clear()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            _objects.Clear();
            _targets.Clear();
        }
    }
}
