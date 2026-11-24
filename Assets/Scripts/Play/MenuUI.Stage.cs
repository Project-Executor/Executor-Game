using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 스테이지 선택 (시안 Executor_Export/02_ui_screens/StageSelect). 바탕화면 위 explorer.exe 창:
    /// 도구줄(← 돌아가기, 경로, 설정) / 왼쪽 Operation 트리(레이어 폴더 > Operation) / 가운데 회로 기판 위 스테이지 노드 /
    /// 오른쪽 선택한 스테이지 정보(▶ 시작, 시나리오 다시 보기) / 상태 표시줄(확대, 진행 상황).
    /// 노드를 누르면 선택, 선택한 노드를 한 번 더 누르거나 ▶ 시작(Enter)으로 시작한다
    /// </summary>
    public partial class MenuUI
    {
        public class StageNode
        {
            public int No;
            public string Title;
            public bool Unlocked;
            public bool Cleared;
            /// <summary>다음에 할 스테이지 (열렸고 아직 클리어하지 않은 첫 스테이지). NEXT 표시</summary>
            public bool Next;
            /// <summary>노드 아래 둘째 줄: "최고 22턴" / "기준 30턴" / "잠김"</summary>
            public string Detail;
            /// <summary>오른쪽 정보: "STAGE 0-5"의 0-5</summary>
            public string Code;
            public string Description;
            public int Par;
            /// <summary>내 기록 (없으면 null)</summary>
            public int? Best;
            /// <summary>받은 별 "2 / 3" (서버 기록이 없으면 null)</summary>
            public string Stars;
            /// <summary>별 조건 줄 (색 태그 포함)</summary>
            public List<string> StarLines = new List<string>();
            /// <summary>맵 미리 보기 (맵이 아직 없으면 null을 돌려준다)</summary>
            public Func<Texture2D> Preview;
            /// <summary>기판 위 자리 (0~1 비율, 왼쪽 위 기준). 관리자 웹에서 정한 값이고 null이면 지그재그 자동 배치</summary>
            public Vector2? BoardPos;
        }

        /// <summary>Operation 트리의 한 줄</summary>
        public class ChapterEntry
        {
            public string Id;
            public string Title;
            public string Subtitle;
            /// <summary>소속 레이어. 같은 값끼리 한 폴더로 묶는다 (목록 순서대로)</summary>
            public string LayerId;
            public string LayerLabel;
            public string LayerDescription;
            public string Status;
            public int Cleared, Total;
            public bool Selectable;
            public bool HasStory;
            public bool Current;
            public bool Frontier;
        }

        public class StageScreen
        {
            public string ChapterId;
            public string Title;
            public string Subtitle;
            /// <summary>주소칸: C: \ Executor \ Layer0_Tutorial \ 홀로그램_시뮬레이션</summary>
            public List<string> Path = new List<string>();
            public List<StageNode> Nodes = new List<StageNode>();
            public List<ChapterEntry> Tree = new List<ChapterEntry>();
            public bool HasStory;
        }

        public Action OnStageBack;
        /// <summary>스테이지 시작 (번호)</summary>
        public Action<int> OnStageSelected;
        public Action<string> OnChapterSelected, OnChapterStory;

        // 창 크기 (시안 left 152, top 40, 1104 x 596)
        private const float ExpLeft = 152 * S, ExpTop = 40 * S, ExpWidth = 1104 * S, ExpHeight = 596 * S, ExpBorder = 2 * S;
        private const float ExpTitleH = 34 * S, ExpToolH = 50 * S, ExpStatusH = 42 * S, TreeW = 220 * S, AsideW = 260 * S;
        private static float ExpBodyTop => ExpBorder + ExpTitleH + ExpToolH;
        private static float ExpBodyH => ExpHeight - ExpBorder * 2 - ExpTitleH - ExpToolH - ExpStatusH;
        private static float BoardW => ExpWidth - ExpBorder * 2 - TreeW - AsideW;

        private Text _expPath, _expHeading, _expSub, _expProgress;
        private RectTransform _expTree, _expMap, _expAside, _expProgressFill;
        private ScrollRect _expTreeScroll;
        private ZoomPan _zoom;
        private string _shownChapter;
        private StageScreen _screen;
        private int _selectedStage;
        private readonly HashSet<string> _collapsedLayers = new HashSet<string>();
        private readonly List<Texture> _previewTextures = new List<Texture>();

        public int SelectedStage => _selectedStage;

        public void ShowStageSelect(StageScreen screen)
        {
            HideAll();
            _stage.SetActive(true);
            SetBadgesAgain();
            _screen = screen;
            _expPath.text = string.Join(" <color=#4b5675>\\</color> ", screen.Path.Select((p, i) =>
                i == screen.Path.Count - 1 ? p : $"<color=#8b98b8>{p}</color>"));
            _expHeading.text = screen.Title;
            _expSub.text = screen.Subtitle;
            int cleared = screen.Nodes.Count(n => n.Cleared);
            _expProgress.text = $"{cleared} / {screen.Nodes.Count}";
            _expProgressFill.anchorMax = new Vector2(screen.Nodes.Count == 0 ? 0 : (float)cleared / screen.Nodes.Count, 1);
            if (_shownChapter != screen.ChapterId)
            {
                _zoom.ResetView();
                StageNode pick = screen.Nodes.FirstOrDefault(n => n.Next) ?? screen.Nodes.LastOrDefault(n => n.Unlocked)
                                 ?? screen.Nodes.FirstOrDefault();
                _selectedStage = pick?.No ?? 0;
            }
            _shownChapter = screen.ChapterId;
            BuildTree();
            DrawStageMap();
            DrawAside();
        }

        /// <summary>스테이지를 고른다 (오른쪽 정보가 바뀐다). 키보드 숫자로도 부른다</summary>
        public void SelectStage(int no)
        {
            if (_screen == null || _screen.Nodes.All(n => n.No != no)) return;
            _selectedStage = no;
            DrawStageMap();
            DrawAside();
        }

        /// <summary>선택한 스테이지 시작 (열린 경우만)</summary>
        public void StartSelectedStage()
        {
            StageNode node = _screen?.Nodes.FirstOrDefault(n => n.No == _selectedStage);
            if (node != null && node.Unlocked) OnStageSelected?.Invoke(node.No);
        }

        /// <summary>스테이지 지도 확대·축소 (키보드·버튼용). factor 1이면 원래 크기로</summary>
        public void ZoomStageMap(float factor)
        {
            if (Mathf.Approximately(factor, 1f)) _zoom.ResetView(); else _zoom.ZoomBy(factor);
        }

        private void SetBadgesAgain()
        {
            SetMailCount(_mailCount);
            SetFriendCount(_friendCount);
        }

        private GameObject BuildStage()
        {
            var screen = Fill(_root, "StageSelect", Night, raycast: true);
            Inset(screen, 0, 0, 0, 0);
            Sprite wallpaper = ArtCatalog.Illustration("main_desktop");
            if (wallpaper != null) Cover(screen.rectTransform, wallpaper);
            BuildDesktopIcons(screen.transform);

            RectTransform win = Frame(screen.transform, "Explorer", Ink, WindowLine, ExpBorder, 6 * S);
            At(win, ExpLeft, ExpTop, ExpWidth, ExpHeight);
            WindowTitle(win, "explorer.exe — 스테이지 선택", ExpWidth, ExpBorder);
            BuildToolbar(win);
            BuildTreePanel(win);
            BuildBoard(win);
            BuildAsidePanel(win);
            BuildStatusBar(win);
            BorderOnTop(win);

            BuildTaskbar(screen.transform, new[] { ("executor.exe", (Action)(() => OnStageBack?.Invoke())), ("explorer.exe", (Action)null) });
            return screen.gameObject;
        }

        // ---------------- 도구줄 ----------------
        private void BuildToolbar(RectTransform win)
        {
            float inner = ExpWidth - ExpBorder * 2;
            Image bar = Fill(win, "Toolbar", Night);
            At(bar, ExpBorder, ExpBorder + ExpTitleH, inner, ExpToolH - 2 * S);
            Image line = Fill(win, "ToolbarLine", TrackLine);
            At(line, ExpBorder, ExpBorder + ExpTitleH + ExpToolH - 2 * S, inner, 2 * S);

            const float h = 32 * S, pad = 12 * S, gap = 8 * S;
            float top = (ExpToolH - 2 * S - h) / 2;
            GhostButton(bar.transform, "← 돌아가기", 12 * S, () => OnStageBack?.Invoke(), out RectTransform back);
            const float backW = 100 * S;
            At(back, pad, top, backW, h);

            float pathLeft = pad + backW + gap;
            float pathW = inner - pathLeft - gap - h - pad;
            Image path = Fill(bar.transform, "Path", Ink);
            At(path, pathLeft, top, pathW, h);
            Border(path.transform, TrackLine, 2 * S);
            _expPath = Label(path.transform, "", 12 * S, Fg, TextAnchor.MiddleLeft, UiKit.Mono);
            Inset(_expPath, 12 * S, 0, 12 * S, 0);

            RectTransform settings = Node(bar.transform, "Settings");
            At(settings, inner - pad - h, top, h, h);
            HoverArea(settings, "Hover", Hex(0xeaf2ff, 0.08f), () => OnSettings?.Invoke(), out Image area);
            Inset(area, 0, 0, 0, 0);
            Image gear = Icon(settings, "desktop/settings", 20 * S);
            if (gear != null) At(gear, (h - 20 * S) / 2, (h - 20 * S) / 2, 20 * S, 20 * S);
        }

        // ---------------- 왼쪽 Operation 트리 ----------------
        private void BuildTreePanel(RectTransform win)
        {
            Image panel = Fill(win, "Tree", Night);
            At(panel, ExpBorder, ExpBodyTop, TreeW - 2 * S, ExpBodyH);
            Image line = Fill(win, "TreeLine", TrackLine);
            At(line, ExpBorder + TreeW - 2 * S, ExpBodyTop, 2 * S, ExpBodyH);

            Text heading = Label(panel.transform, "OPERATION", 12 * S, Muted, TextAnchor.MiddleLeft, bold: true);
            At(heading, 16 * S, 12 * S, TreeW - 32 * S, 16 * S);

            // 스크롤 목록
            RectTransform viewport = Node(panel.transform, "Viewport");
            Inset(viewport, 8 * S, 36 * S, 8 * S, 12 * S);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);
            _expTree = Node(viewport, "Content");
            _expTree.anchorMin = new Vector2(0, 1);
            _expTree.anchorMax = new Vector2(1, 1);
            _expTree.pivot = new Vector2(0.5f, 1);
            _expTree.offsetMin = _expTree.offsetMax = Vector2.zero;
            _expTreeScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _expTreeScroll.content = _expTree;
            _expTreeScroll.viewport = viewport;
            _expTreeScroll.horizontal = false;
            _expTreeScroll.movementType = ScrollRect.MovementType.Clamped;
            _expTreeScroll.scrollSensitivity = 30;
        }

        private void BuildTree()
        {
            UiKit.Clear(_expTree);
            float width = TreeW - 2 * S - 16 * S;
            float y = 0;
            var layers = new List<string>();
            foreach (ChapterEntry e in _screen.Tree)
                if (!layers.Contains(e.LayerId)) layers.Add(e.LayerId);

            foreach (string layerId in layers)
            {
                List<ChapterEntry> ops = _screen.Tree.Where(e => e.LayerId == layerId).ToList();
                bool locked = ops.All(o => !o.Selectable);
                bool open = !locked && (!_collapsedLayers.Contains(layerId) || ops.Any(o => o.Current));
                if (y > 0) y += 8 * S;

                // 레이어 폴더 줄
                const float rowH = 32 * S;
                RectTransform row = Node(_expTree, "Layer:" + layerId);
                At(row, 0, y, width, rowH);
                if (!locked)
                {
                    string id = layerId;
                    HoverArea(row, "Hover", Hex(0x22d3ee, 0.10f), () =>
                    {
                        if (!_collapsedLayers.Remove(id)) _collapsedLayers.Add(id);
                        BuildTree();
                    }, out Image area);
                    Inset(area, 0, 0, 0, 0);
                }
                Text arrow = Label(row, open ? "▾" : "▸", 12 * S, locked ? Dim : Muted, TextAnchor.MiddleLeft);
                At(arrow, 8 * S, 0, 12 * S, rowH);
                Image folder = Icon(row, locked ? "desktop/folder_locked" : "desktop/folder", 16 * S);
                if (folder != null) At(folder, 24 * S, (rowH - 16 * S) / 2, 16 * S, 16 * S);
                string label = ops[0].LayerLabel + (locked ? " · 잠김" : "");
                Text name = Label(row, label, 12 * S, locked ? Dim : Fg, TextAnchor.MiddleLeft, bold: true);
                At(name, 48 * S, 0, width - 48 * S, rowH);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                y += rowH + 4 * S;
                if (!open) continue;

                // Operation 줄
                foreach (ChapterEntry op in ops)
                {
                    const float indent = 24 * S;
                    float opW = width - indent;
                    RectTransform opRow = Node(_expTree, "Op:" + op.Id);
                    if (op.Current)
                    {
                        const float h = 40 * S;
                        At(opRow, indent, y, opW, h);
                        Image bg = Fill(opRow, "Bg", Hex(0x22d3ee, 0.14f));
                        Inset(bg, 0, 0, 0, 0);
                        Image bar = Fill(opRow, "Bar", Cyan);
                        At(bar, 0, 0, 2 * S, h);
                        Text title = Label(opRow, op.Title, 12 * S, Fg, TextAnchor.MiddleLeft);
                        Inset(title, 8 * S, 0, 48 * S, 0);
                        Text count = Label(opRow, $"{op.Cleared}/{op.Total}", 12 * S, Cyan, TextAnchor.MiddleRight);
                        Inset(count, 0, 0, 8 * S, 0);
                        y += h + 4 * S;
                        continue;
                    }
                    const float twoLine = 6 * S + 16 * S + 2 * S + 16 * S + 6 * S;
                    At(opRow, indent, y, opW, twoLine);
                    if (op.Selectable)
                    {
                        string id = op.Id;
                        HoverArea(opRow, "Hover", Hex(0x22d3ee, 0.10f), () => OnChapterSelected?.Invoke(id), out Image area);
                        Inset(area, 0, 0, 0, 0);
                    }
                    Text t1 = Label(opRow, op.Title, 12 * S, op.Selectable ? Fg : Dim, TextAnchor.MiddleLeft);
                    At(t1, 8 * S, 6 * S, opW - 16 * S, 16 * S);
                    Color statusColor = !op.Selectable ? Dim
                        : op.Total > 0 && op.Cleared >= op.Total ? Green
                        : op.Cleared > 0 || op.Frontier ? Orange : Muted;
                    Text t2 = Label(opRow, op.Status, 12 * S, statusColor, TextAnchor.MiddleLeft);
                    At(t2, 8 * S, 6 * S + 16 * S + 2 * S, opW - 16 * S, 16 * S);
                    y += twoLine + 4 * S;
                }
            }
            _expTree.sizeDelta = new Vector2(0, y);
        }

        // ---------------- 가운데 회로 기판 ----------------
        private void BuildBoard(RectTransform win)
        {
            float left = ExpBorder + TreeW;
            RectTransform board = Node(win, "Board");
            At(board, left, ExpBodyTop, BoardW, ExpBodyH);
            board.gameObject.AddComponent<RectMask2D>();
            // 바탕: #121a3c + 32px(×1.5) 격자선 #0e1532
            var grid = new GameObject("Grid", typeof(RectTransform), typeof(RawImage));
            grid.transform.SetParent(board, false);
            var raw = grid.GetComponent<RawImage>();
            raw.texture = GridTexture();
            raw.uvRect = new Rect(0, 0, BoardW / (32 * S), ExpBodyH / (32 * S));
            raw.raycastTarget = true;
            Inset(raw, 0, 0, 0, 0);

            // 확대 가능한 지도 (노드·선). 지도 좌표는 기판 왼쪽 위 기준
            RectTransform viewport = Node(board, "Viewport");
            Inset(viewport, 0, 0, 0, 0);
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);
            _expMap = Node(viewport, "Map");
            Inset(_expMap, 0, 0, 0, 0);
            _zoom = viewport.gameObject.AddComponent<ZoomPan>();
            _zoom.Content = _expMap;
            _zoom.Grid = raw;
            _zoom.GridTile = 32 * S;
            _zoom.PanSlack = 0.35f; // 확대하지 않아도 빈 곳을 끌어 기판을 옮길 수 있다 (가로·세로 35%까지)

            _expHeading = Label(board, "", 24 * S, Fg, TextAnchor.UpperLeft, bold: true);
            At(_expHeading, 24 * S, 20 * S, BoardW - 48 * S, 28 * S);
            _expSub = Label(board, "", 12 * S, Muted, TextAnchor.UpperLeft);
            At(_expSub, 24 * S, 54 * S, BoardW - 48 * S, 16 * S);
        }

        private static Texture2D _gridTexture;

        private static Texture2D GridTexture()
        {
            if (_gridTexture != null) return _gridTexture;
            const int n = 32;
            _gridTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[n * n];
            Color32 bg = Hex(0x121a3c), line = Hex(0x0e1532);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                pixels[y * n + x] = x == 0 || y == n - 1 ? line : bg; // 위쪽 줄(텍스처의 맨 위 행) + 왼쪽 줄
            _gridTexture.SetPixels32(pixels);
            _gridTexture.Apply();
            return _gridTexture;
        }

        /// <summary>노드 자리. 관리자가 정한 자리(BoardPos)가 있으면 그곳, 없으면 지그재그 자동 배치</summary>
        private static List<Vector2> NodeCenters(List<StageNode> nodes)
        {
            List<Vector2> auto = AutoCenters(nodes.Count);
            var list = new List<Vector2>();
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2? p = nodes[i].BoardPos;
                list.Add(p.HasValue
                    ? new Vector2(Mathf.Clamp01(p.Value.x) * BoardW, Mathf.Clamp01(p.Value.y) * ExpBodyH)
                    : auto[i]);
            }
            return list;
        }

        /// <summary>자동 배치 (시안의 6개 위치를 되풀이). 많으면 간격을 줄여 기판 안에 넣는다. 관리자 웹 stageBoard.ts defaultCenter와 같다</summary>
        private static List<Vector2> AutoCenters(int count)
        {
            // 시안: 노드 칸(가로 100) 왼쪽 6 + i*101.6, 위 30 + [268,138,298,118,278,158] + 노드 반(32)
            float[] tops = { 268, 138, 298, 118, 278, 158 };
            float step = 101.6f * S;
            float usable = BoardW - 2 * 56 * S;
            if (count > 1 && step * (count - 1) > usable) step = usable / (count - 1);
            float start = count > 1 && step * (count - 1) < usable ? (BoardW - step * (count - 1)) / 2 : 56 * S;
            var list = new List<Vector2>();
            for (int i = 0; i < count; i++)
                list.Add(new Vector2(start + i * step, (30 + tops[i % tops.Length] + 32) * S));
            return list;
        }

        private void DrawStageMap()
        {
            UiKit.Clear(_expMap);
            List<StageNode> nodes = _screen.Nodes;
            if (nodes.Count == 0)
            {
                Text empty = Label(_expMap, "준비 중인 Operation입니다.\n왼쪽에서 다른 Operation을 고를 수 있습니다.", 12 * S, Muted, TextAnchor.MiddleCenter);
                Inset(empty, 0, 0, 0, 0);
                return;
            }
            List<Vector2> centers = NodeCenters(nodes);
            for (int i = 0; i + 1 < nodes.Count; i++)
            {
                bool placed = nodes[i].BoardPos.HasValue || nodes[i + 1].BoardPos.HasValue;
                Wire(_expMap, WirePoints(centers[i], centers[i + 1], placed), nodes[i + 1].Unlocked);
            }
            for (int i = 0; i < nodes.Count; i++) StageButton(nodes[i], centers[i]);
        }

        /// <summary>
        /// 두 노드를 잇는 꺾은선 (점 4개). 보통은 가로 - 세로 - 가로로 옆면끼리 잇고,
        /// 관리자가 놓은 노드가 위아래로 겹쳐 있으면 세로 - 가로 - 세로로 아랫면·윗면을 잇는다. 관리자 웹 stageBoard.ts wirePoints와 같다
        /// </summary>
        private static Vector2[] WirePoints(Vector2 a, Vector2 b, bool placed)
        {
            const float half = 32 * S;
            float dx = b.x - a.x, dy = b.y - a.y;
            if (placed && Mathf.Abs(dx) < half * 2 + 8 * S)
            {
                float sy = dy >= 0 ? 1 : -1;
                var from = new Vector2(a.x, a.y + sy * half);
                var to = new Vector2(b.x, b.y - sy * half);
                float midY = (from.y + to.y) / 2;
                return new[] { from, new Vector2(from.x, midY), new Vector2(to.x, midY), to };
            }
            float sx = dx >= 0 ? 1 : -1;
            var start = new Vector2(a.x + sx * half, a.y);
            var end = new Vector2(b.x - sx * half, b.y);
            float midX = (start.x + end.x) / 2;
            return new[] { start, new Vector2(midX, start.y), new Vector2(midX, end.y), end };
        }

        /// <summary>회로 선. 열린 길은 하늘색(검은 테두리), 잠긴 길은 회색 점선</summary>
        private static void Wire(RectTransform map, Vector2[] points, bool active)
        {
            for (int i = 0; i + 1 < points.Length; i++)
            {
                if (active)
                {
                    Segment(map, points[i], points[i + 1], 7 * S, Ink, 0);
                    Segment(map, points[i], points[i + 1], 3 * S, Cyan, 0);
                }
                else Segment(map, points[i], points[i + 1], 3 * S, Dim, 6 * S);
            }
        }

        /// <summary>가로·세로 선 하나 (dash &gt; 0이면 점선). 좌표는 지도 왼쪽 위 기준 (y는 아래로)</summary>
        private static void Segment(RectTransform map, Vector2 a, Vector2 b, float thick, Color color, float dash)
        {
            bool horizontal = Mathf.Abs(a.y - b.y) < 0.01f;
            float length = horizontal ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.y - a.y);
            Vector2 start = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
            float pos = 0;
            while (pos < length + 0.01f)
            {
                float len = dash > 0 ? Mathf.Min(dash, length - pos) : length;
                Image seg = Fill(map, "Wire", color);
                if (horizontal) At(seg, start.x + pos - (dash > 0 ? 0 : thick / 2), start.y - thick / 2, len + (dash > 0 ? 0 : thick), thick);
                else At(seg, start.x - thick / 2, start.y + pos, thick, len);
                if (dash <= 0) break;
                pos += dash * 2;
            }
        }

        private void StageButton(StageNode node, Vector2 center)
        {
            bool selected = node.No == _selectedStage;
            const float size = 64 * S, border = 3 * S;
            RectTransform root = Node(_expMap, "Stage" + node.No);
            At(root, center.x - size / 2, center.y - size / 2, size, size);

            if (selected)
            {
                // 빛: 바깥 4px 링(25%) + 더 넓은 옅은 링
                Image glow2 = Fill(root, "Glow2", Hex(0x22d3ee, 0.12f));
                Inset(glow2, -12 * S, -12 * S, -12 * S, -12 * S);
                Image glow = Fill(root, "Glow", Hex(0x22d3ee, 0.25f));
                Inset(glow, -4 * S, -4 * S, -4 * S, -4 * S);
            }
            Color bg = selected ? Cyan : node.Unlocked ? Track : Hex(0x10132e);
            Color line = selected ? CyanLight : node.Unlocked ? Cyan : TrackLine;
            Color fg = selected ? Ink : node.Unlocked ? Fg : Dim;
            Image face = Fill(root, "Face", Color.white);
            Inset(face, 0, 0, 0, 0);
            int no = node.No;
            Clickable(face, () =>
            {
                if (_selectedStage == no) StartSelectedStage(); else SelectStage(no);
            }, bg, Color.Lerp(bg, Color.white, 0.15f), Color.Lerp(bg, Color.black, 0.15f));
            Border(root, line, border);
            Text num = Label(root, node.No.ToString(), 36 * S, fg, TextAnchor.MiddleCenter, UiKit.Mono);
            Inset(num, 0, 0, 0, 0);

            // 오른쪽 위 표시: 클리어 ✓ / 잠김 자물쇠
            if (node.Cleared || !node.Unlocked)
            {
                RectTransform badge = Node(root, "Badge");
                At(badge, size - 18 * S + 8 * S - 2 * S, -8 * S - 2 * S, 22 * S, 22 * S);
                Image ring = Fill(badge, "Ring", Ink);
                Inset(ring, 0, 0, 0, 0);
                Image fill = Fill(badge, "Fill", node.Cleared ? Green : TrackLine);
                Inset(fill, 2 * S, 2 * S, 2 * S, 2 * S);
                Image mark = Icon(badge, node.Cleared ? "desktop/check" : "desktop/lock", node.Cleared ? 12 * S : 10 * S);
                if (mark != null)
                {
                    float m = node.Cleared ? 12 * S : 10 * S;
                    At(mark, (22 * S - m) / 2, (22 * S - m) / 2, m, m);
                }
            }
            // 위쪽 NEXT 표시
            if (node.Next)
            {
                Image tag = Fill(root, "Next", Orange);
                Text t = Label(tag.transform, "NEXT", 12 * S, Ink, TextAnchor.MiddleCenter, bold: true);
                float w = t.preferredWidth + 12 * S;
                At(tag, (size - w) / 2, -28 * S, w, 20 * S);
                Inset(t, 0, 0, 0, 0);
            }

            // 아래 글자: "5 · 종료" / 기록
            Color captionColor = selected ? CyanLight : node.Unlocked ? Fg : Dim;
            Text cap = Label(_expMap, $"{node.No} · {node.Title}", 12 * S, captionColor, TextAnchor.UpperCenter);
            At(cap, center.x - 50 * S, center.y + size / 2 + 8 * S, 100 * S, 16 * S);
            Text detail = Label(_expMap, node.Detail ?? "", 12 * S, node.Unlocked ? Muted : Dim, TextAnchor.UpperCenter);
            At(detail, center.x - 50 * S, center.y + size / 2 + 8 * S + 16 * S, 100 * S, 16 * S);
        }

        // ---------------- 오른쪽 스테이지 정보 ----------------
        private void BuildAsidePanel(RectTransform win)
        {
            float left = ExpBorder + TreeW + BoardW;
            Image line = Fill(win, "AsideLine", TrackLine);
            At(line, left, ExpBodyTop, 2 * S, ExpBodyH);
            Image panel = Fill(win, "Aside", Night);
            At(panel, left + 2 * S, ExpBodyTop, AsideW - 2 * S, ExpBodyH);
            _expAside = panel.rectTransform;
        }

        private void DrawAside()
        {
            UiKit.Clear(_expAside);
            foreach (Texture t in _previewTextures) Destroy(t);
            _previewTextures.Clear();
            StageNode node = _screen.Nodes.FirstOrDefault(n => n.No == _selectedStage);
            float width = AsideW - 2 * S - 40 * S;
            const float pad = 20 * S, gap = 16 * S;
            if (node == null) return;
            float y = pad;

            Text code = Label(_expAside, $"STAGE {node.Code}", 12 * S, Cyan, TextAnchor.UpperLeft, UiKit.Mono);
            At(code, pad, y, width, 16 * S);
            y += 16 * S + 4 * S;
            Text title = Label(_expAside, node.Title, 24 * S, Fg, TextAnchor.UpperLeft, bold: true);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            At(title, pad, y, width, 28 * S);
            title.rectTransform.sizeDelta = new Vector2(width, Mathf.Max(28 * S, title.preferredHeight));
            y += title.rectTransform.sizeDelta.y;
            if (!string.IsNullOrEmpty(node.Description))
            {
                y += 6 * S;
                Text desc = Label(_expAside, node.Description, 12 * S, Muted, TextAnchor.UpperLeft);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                desc.lineSpacing = 1.2f;
                At(desc, pad, y, width, 18 * S);
                float h = desc.preferredHeight;
                desc.rectTransform.sizeDelta = new Vector2(width, h);
                y += h;
            }
            y += gap;

            // 맵 미리 보기
            Texture2D preview = node.Unlocked ? node.Preview?.Invoke() : null;
            if (preview != null)
            {
                _previewTextures.Add(preview);
                float maxW = width, maxH = 80 * S;
                float scale = Mathf.Min(maxW / preview.width, maxH / preview.height);
                float pw = preview.width * scale, ph = preview.height * scale;
                RectTransform box = Node(_expAside, "Preview");
                At(box, pad + (width - pw) / 2 - 4 * S, y, pw + 8 * S, ph + 8 * S);
                Image boxBg = Fill(box, "Bg", Ink);
                Inset(boxBg, 0, 0, 0, 0);
                var go = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(box, false);
                var raw = go.GetComponent<RawImage>();
                raw.texture = preview;
                raw.raycastTarget = false;
                Inset(raw, 4 * S, 4 * S, 4 * S, 4 * S);
                Border(box, TrackLine, 2 * S);
                y += ph + 8 * S + gap;
            }

            // 기준 턴 / 내 기록 / 별
            var rows = new List<(string, string, bool)> { ("기준 턴", node.Par > 0 ? node.Par.ToString() : "—", node.Par > 0) };
            rows.Add(("내 기록", node.Best?.ToString() ?? "—", node.Best != null));
            if (node.Stars != null) rows.Add(("별", node.Stars, true));
            foreach (var (label, value, has) in rows)
            {
                Text l = Label(_expAside, label, 12 * S, Muted, TextAnchor.MiddleLeft);
                At(l, pad, y, width / 2, 16 * S);
                Text v = Label(_expAside, value, 12 * S, has ? Fg : Dim, TextAnchor.MiddleRight, UiKit.Mono);
                At(v, pad + width / 2, y, width / 2, 16 * S);
                y += 16 * S + 6 * S;
            }
            y += gap - 6 * S;

            // 별 조건
            if (node.StarLines.Count > 0)
            {
                // 긴 조건은 상자 폭에서 줄을 바꾸고, 줄 수만큼 상자를 높인다
                const float boxPad = 12 * S, lineH = 16 * S, lineGap = 6 * S;
                float textW = width - boxPad * 2;
                Image box = Fill(_expAside, "Stars", Ink);
                float ty = boxPad;
                for (int i = 0; i < node.StarLines.Count; i++)
                {
                    Text t = Label(box.transform, node.StarLines[i], 12 * S, Fg, TextAnchor.UpperLeft);
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.lineSpacing = 1.15f;
                    At(t, boxPad, ty, textW, lineH);
                    float h = Mathf.Max(lineH, t.preferredHeight);
                    t.rectTransform.sizeDelta = new Vector2(textW, h);
                    ty += h + (i < node.StarLines.Count - 1 ? lineGap : 0);
                }
                At(box, pad, y, width, ty + boxPad);
            }

            // 아래: ▶ 시작 / 시나리오 다시 보기
            float bottom = ExpBodyH - pad;
            if (_screen.HasStory)
            {
                GhostButton(_expAside, "시나리오 다시 보기", 12 * S, () => OnChapterStory?.Invoke(_screen.ChapterId), out RectTransform story);
                At(story, pad, bottom - 36 * S, width, 36 * S);
                bottom -= 36 * S + 8 * S;
            }
            Button start = PrimaryButton(_expAside, node.Unlocked ? "▶ 시작" : "잠김", 24 * S, StartSelectedStage, out RectTransform startRt);
            At(startRt, pad, bottom - 48 * S, width, 48 * S);
            start.interactable = node.Unlocked;
        }

        // ---------------- 상태 표시줄 ----------------
        private void BuildStatusBar(RectTransform win)
        {
            float inner = ExpWidth - ExpBorder * 2;
            float top = ExpHeight - ExpBorder - ExpStatusH;
            Image line = Fill(win, "StatusLine", TrackLine);
            At(line, ExpBorder, top, inner, 2 * S);
            Image bar = Fill(win, "Status", Night);
            At(bar, ExpBorder, top + 2 * S, inner, ExpStatusH - 2 * S);

            const float h = 28 * S, gap = 8 * S;
            float y = (ExpStatusH - 2 * S - h) / 2;
            float x = 12 * S;
            foreach (var (label, factor, w) in new[] { ("+", 1.25f, 28 * S), ("−", 0.8f, 28 * S), ("맞춤", 1f, 48 * S) })
            {
                float f = factor;
                Image b = Fill(bar.transform, "Zoom" + label, Color.white);
                At(b, x, y, w, h);
                Clickable(b, () => ZoomStageMap(f), Ink, Hex(0x1e1942));
                Border(b.transform, TrackLine, 2 * S);
                Text t = Label(b.transform, label, 12 * S, Fg, TextAnchor.MiddleCenter);
                Inset(t, 0, 0, 0, 0);
                x += w + gap;
            }
            Text help = Label(bar.transform, "휠 · +/− 확대 · 빈 곳 끌어 이동 · 0 맞춤 · 숫자 선택 · Enter 시작", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(help, x + 4 * S, 0, 700 * S, ExpStatusH - 2 * S);

            // 오른쪽: 진행 상황 ▬▬▬ 4 / 6
            _expProgress = Label(bar.transform, "", 12 * S, Fg, TextAnchor.MiddleRight);
            AtRight(_expProgress, 12 * S, 0, 60 * S, ExpStatusH - 2 * S);
            Image track = Fill(bar.transform, "Track", Track);
            AtRight(track, 12 * S + 60 * S + gap, (ExpStatusH - 2 * S - 8 * S) / 2, 160 * S, 8 * S);
            Border(track.transform, TrackLine, 1 * S);
            Image fill = Fill(track.transform, "Fill", Cyan);
            _expProgressFill = fill.rectTransform;
            _expProgressFill.anchorMin = Vector2.zero;
            _expProgressFill.anchorMax = new Vector2(0, 1);
            _expProgressFill.offsetMin = _expProgressFill.offsetMax = Vector2.zero;
            Text label2 = Label(bar.transform, "진행 상황", 12 * S, Muted, TextAnchor.MiddleRight);
            AtRight(label2, 12 * S + 60 * S + gap + 160 * S + gap, 0, 120 * S, ExpStatusH - 2 * S);
        }
    }
}
