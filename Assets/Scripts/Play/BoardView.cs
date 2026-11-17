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
            Sync();
            SnapAll();
            FitCamera();
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
                sr.gameObject.SetActive(!obj.IsRemoved);
                _targets[sr.transform] = ToWorld(obj.Pos);
                // 블록은 남은 내구도만큼 진하게
                if (obj.Type == MapObjectTypes.Breakable)
                {
                    int max = _board.Data.Objects.Find(o => o.Id == obj.Id)?.Hp ?? 1;
                    float t = max <= 1 ? 1f : 0.45f + 0.55f * obj.Hp / max;
                    sr.color = new Color(t, t, t, 1f);
                }
            }
        }

        /// <summary>워프: 미끄러지지 않고 바로 그 상태로 옮긴다</summary>
        public void SnapToBoard()
        {
            Sync();
            foreach (var pair in _targets)
            {
                if (pair.Key != null) pair.Key.position = pair.Value;
            }
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

        /// <summary>맵 전체(+여백 반 칸)가 GameUI.GameArea 안에 들어오도록 카메라 크기·위치를 맞춘다</summary>
        private void FitCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            Rect area = GameUI.GameArea;
            float aspect = cam.aspect;
            float size = Mathf.Max((_board.Height + 1f) / (2f * area.height), (_board.Width + 1f) / (2f * aspect * area.width));
            cam.orthographicSize = size;
            var mapCenter = new Vector2((_board.Width - 1) / 2f, (_board.Height - 1) / 2f);
            var offset = new Vector2((area.center.x - 0.5f) * 2f * size * aspect, (area.center.y - 0.5f) * 2f * size);
            cam.transform.position = new Vector3(mapCenter.x - offset.x, mapCenter.y - offset.y, -10f);
            cam.backgroundColor = new Color32(0x2a, 0x2f, 0x37, 0xff);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private void Clear()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            _objects.Clear();
            _targets.Clear();
        }
    }
}
