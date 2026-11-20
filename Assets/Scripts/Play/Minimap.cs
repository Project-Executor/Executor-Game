using Executor.Data;
using Executor.Game;
using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 미니맵 그림. Board 상태를 칸당 CellPixels 픽셀의 텍스처로 그린다 (왼쪽 위 미니맵, 전체 맵 팝업에 같이 쓴다).
    /// 체크포인트는 도착 전(흐림) / 안정(청록) / 불안정(주황)으로 구분한다.
    /// </summary>
    public static class Minimap
    {
        private const int CellPixels = 4;

        private static readonly Color32 Wall = new Color32(0x1a, 0x20, 0x29, 0xff);
        private static readonly Color32 Floor = new Color32(0x56, 0x62, 0x72, 0xff);
        private static readonly Color32 Player = new Color32(0xff, 0xff, 0xff, 0xff);
        private static readonly Color32 PlayerEdge = new Color32(0x2f, 0x6f, 0xde, 0xff);
        private static readonly Color32 Exit = new Color32(0x3c, 0xcf, 0x7a, 0xff);
        private static readonly Color32 Crate = new Color32(0xb0, 0x7a, 0x3c, 0xff);
        private static readonly Color32 Breakable = new Color32(0x8f, 0xc8, 0xf0, 0xff); // 방화벽 (얼음빛 하늘색)
        private static readonly Color32 Key = new Color32(0xff, 0xd2, 0x3f, 0xff);
        private static readonly Color32 Door = new Color32(0xc0, 0x45, 0x55, 0xff);
        private static readonly Color32 Npc = new Color32(0xb0, 0x7c, 0xf0, 0xff);
        private static readonly Color32 CheckpointNew = new Color32(0x2c, 0x7f, 0x8c, 0xff);
        private static readonly Color32 CheckpointStable = new Color32(0x2c, 0xd5, 0xe8, 0xff);
        private static readonly Color32 CheckpointUnstable = new Color32(0xff, 0x8c, 0x3c, 0xff);
        private static readonly Color32 Hidden = new Color32(0xff, 0x5f, 0xb0, 0xff);
        private static readonly Color32 Anomaly = new Color32(0xe0, 0x36, 0x2c, 0xff);
        private static readonly Color32 FakeWall = new Color32(0x3a, 0x45, 0x55, 0xff);
        private static readonly Color32 ComputerInfected = new Color32(0xc2, 0x36, 0x5a, 0xff);
        private static readonly Color32 ComputerRestored = new Color32(0x5f, 0xd1, 0xb0, 0xff);
        /// <summary>아직 밝혀지지 않은 칸</summary>
        private static readonly Color32 Fog = new Color32(0x0b, 0x0e, 0x13, 0xff);

        /// <summary>그림 설명 (전체 맵 팝업 아래에 쓴다)</summary>
        public const string Legend =
            "흰 점: 나 · 초록: 출구 · 청록: 안정 체크포인트 · 주황: 불안정 · 흐린 청록: 아직 안 간 체크포인트 · 분홍: 데이터 파편(찾은 것만) · 진홍: 이상 개체\n"
            + "노랑: 열쇠 · 자주: 문 · 갈색: 상자 · 하늘색: 방화벽 · 보라: 인물 · 어두운 회색: 찾은 가짜 벽 · 빨강/민트: 감염/복구된 컴퓨터 · 검정: 아직 안 가 본 곳";

        /// <summary>Board를 텍스처로 그린다. reuse가 같은 크기면 다시 쓴다</summary>
        public static Texture2D Render(Board board, Texture2D reuse = null)
        {
            int w = board.Width * CellPixels, h = board.Height * CellPixels;
            Texture2D tex = reuse != null && reuse.width == w && reuse.height == h
                ? reuse
                : new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * h];

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var cell = new GridPos(x, y);
                Fill(pixels, w, board.Height, x, y, !board.IsExplored(cell) ? Fog : board.IsWall(cell) ? Wall : Floor, 0);
            }

            foreach (BoardObject o in board.Objects)
            {
                // 밝혀지지 않은 칸의 오브젝트는 그리지 않는다
                if (o.IsRemoved || !board.IsExplored(o.Pos)) continue;
                // 찾지 못한 가짜 벽은 벽으로 그린다
                if (o.Type == MapObjectTypes.FakeWall && !o.IsRevealed)
                {
                    Fill(pixels, w, board.Height, o.Pos.X, o.Pos.Y, Wall, 0);
                    continue;
                }
                if (!o.IsRevealed) continue;
                Fill(pixels, w, board.Height, o.Pos.X, o.Pos.Y, ColorOf(board, o), 0);
            }

            // 나: 테두리 + 가운데 흰색
            GridPos p = board.PlayerPos;
            Fill(pixels, w, board.Height, p.X, p.Y, PlayerEdge, 0);
            Fill(pixels, w, board.Height, p.X, p.Y, Player, 1);

            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        private static Color32 ColorOf(Board board, BoardObject o)
        {
            switch (o.Type)
            {
                case MapObjectTypes.Exit: return Exit;
                case MapObjectTypes.Crate: return Crate;
                case MapObjectTypes.Breakable: return Breakable;
                case MapObjectTypes.Key: return Key;
                case MapObjectTypes.Door: return Door;
                case MapObjectTypes.Npc: return Npc;
                case MapObjectTypes.Hidden: return Hidden;
                case MapObjectTypes.Anomaly: return Anomaly;
                case MapObjectTypes.FakeWall: return FakeWall;
                case MapObjectTypes.Computer: return o.IsRestored ? ComputerRestored : ComputerInfected;
                case MapObjectTypes.Checkpoint:
                    bool? stable = board.StabilityOf(o.Order);
                    return stable == null ? CheckpointNew : stable.Value ? CheckpointStable : CheckpointUnstable;
                default: return Floor;
            }
        }

        /// <summary>칸 하나를 칠한다. inset만큼 안쪽만 (map_data y는 아래로, 텍스처 y는 위로 커진다)</summary>
        private static void Fill(Color32[] pixels, int texWidth, int mapHeight, int x, int y, Color32 color, int inset)
        {
            int baseX = x * CellPixels, baseY = (mapHeight - 1 - y) * CellPixels;
            for (int dy = inset; dy < CellPixels - inset; dy++)
            for (int dx = inset; dx < CellPixels - inset; dx++)
            {
                pixels[(baseY + dy) * texWidth + baseX + dx] = color;
            }
        }
    }
}
