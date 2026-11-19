using System.Collections.Generic;
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
        private const float MoveSeconds = 0.08f;

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
        private readonly Dictionary<BoardObject, SpriteRenderer> _objects = new Dictionary<BoardObject, SpriteRenderer>();
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

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var pos = new GridPos(x, y);
                Spawn(board.IsWall(pos) ? "wall" : "floor", board.IsWall(pos) ? ArtCatalog.Wall : ArtCatalog.Floor, pos, 0);
            }
            foreach (BoardObject obj in board.Objects)
            {
                string characterId = obj.Type == MapObjectTypes.Npc && obj.Name != null
                                     && ArtCatalog.SpeakerToCharacter.TryGetValue(obj.Name, out string id) ? id : null;
                SpriteRenderer sr = Spawn(obj.Id, ArtCatalog.ForObject(obj.Type, characterId), obj.Pos, 1);
                _objects[obj] = sr;
            }
            _player = Spawn("player", ArtCatalog.Player, board.PlayerPos, 2).transform;
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
            _targets[_player] = ToWorld(_board.PlayerPos);
            foreach (var pair in _objects)
            {
                BoardObject obj = pair.Key;
                SpriteRenderer sr = pair.Value;
                if (obj.Type == MapObjectTypes.FakeWall)
                {
                    // 가짜 벽: 드러난 뒤에도 화면에서는 계속 진짜 벽과 똑같이 그린다 (드러남은 미니맵에만 표시)
                    sr.gameObject.SetActive(!obj.IsRemoved);
                    sr.sprite = ArtCatalog.Wall;
                }
                else sr.gameObject.SetActive(!obj.IsRemoved && obj.IsRevealed);
                // 컴퓨터: 감염 / 복구
                if (obj.Type == MapObjectTypes.Computer)
                {
                    sr.sprite = obj.IsRestored ? ArtCatalog.ComputerRestored : ArtCatalog.ForObject(obj.Type);
                }
                _targets[sr.transform] = ToWorld(obj.Pos);
                // 블록은 남은 내구도만큼 진하게
                if (obj.Type == MapObjectTypes.Breakable)
                {
                    int max = _board.Data.Objects.Find(o => o.Id == obj.Id)?.Hp ?? 1;
                    float t = max <= 1 ? 1f : 0.45f + 0.55f * obj.Hp / max;
                    sr.color = new Color(t, t, t, 1f);
                }
            }
            UpdateCamera(false);
        }

        /// <summary>워프: 미끄러지지 않고 바로 그 상태로 옮긴다 (카메라도)</summary>
        public void SnapToBoard()
        {
            Sync();
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

        /// <summary>막힌 입력: 그 방향으로 살짝 흔들기</summary>
        public void Bump(Direction direction)
        {
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

            Camera cam = Camera.main;
            if (_hasCamTarget && cam != null)
            {
                float t = 1f - Mathf.Exp(-CameraSharpness * Time.deltaTime);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _camSize, t);
                cam.transform.position = Vector3.Lerp(cam.transform.position, _camPos, t);
            }
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
            cam.backgroundColor = new Color32(0x2a, 0x2f, 0x37, 0xff);
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
