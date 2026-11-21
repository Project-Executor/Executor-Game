using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 플레이 화면 HUD (시안 Executor_Export/02_ui_screens/Game, 1280x720 × 1.5).
    /// 위 가운데: 경로(Layer · Operation) + 스테이지 이름 / 왼쪽 열: minimap, quest.txt(별 조건 + 최근 안내) /
    /// 오른쪽 열: turns(남은 턴 큰 숫자 + 칸 막대), checkpoint, collect(데이터 파편·열쇠), 채팅·설정 / 아래: 키 안내 줄.
    /// 가운데 게임 화면은 <see cref="GameArea"/> 안에 BoardView가 그린다
    /// </summary>
    public partial class GameUI
    {
        public Action OnChat, OnSettings, OnCollect;
        /// <summary>미니맵을 눌렀을 때 (전체 맵 팝업), 퀘스트를 눌렀을 때 (자세히)</summary>
        public Action OnMinimap, OnQuest;

        public enum QuestState { Pending, Done, Warn }

        // 열 배치 (시안 left 24 / 984, top 20, 너비 272, 간격 16)
        private const float ColLeft = 24 * S, ColRight = 984 * S, ColTop = 20 * S, ColW = 272 * S, ColGap = 16 * S;
        private const float PanelBorder = 2 * S, PanelHead = 24 * S + 2 * S, PanelPad = 12 * S;

        private GameObject _hud;
        private Text _hudPath, _hudTitle;
        // 왼쪽
        private RectTransform _minimapPanel, _questPanel;
        private RawImage _minimap;
        private readonly List<Text> _questRows = new List<Text>();
        private RectTransform _guideBox;
        private Text _guideText;
        private IReadOnlyList<(string text, QuestState state)> _questStars = new (string, QuestState)[0];
        private readonly List<string> _guides = new List<string>();
        /// <summary>퀘스트 팝업에 보여 줄 자세한 내용 (목표·별 조건·체크포인트·안내)</summary>
        private string _questDetail = "";
        // 오른쪽
        private RectTransform _turnsPanel, _checkpointPanel, _collectPanel, _buttonRow;
        private Text _turnsNumber, _turnsSegment, _turnsFooter;
        private RectTransform _turnsBar;
        private int _turnsBarCells = -1;
        private RectTransform _checkpointDots;
        private Text _collectCount, _keyCount;
        private GameObject _keyRow;
        private Text _chatLabel;
        // 안내 (토스트)
        private RectTransform _toastBox;
        private readonly Queue<(string text, Color accent)> _toastQueue = new Queue<(string, Color)>();

        public string QuestDetail => _questDetail;

        /// <summary>
        /// 일시정지 (시안 Pause, paused): 스테이지 이름·남은 턴, ▶ 계속하기 [Esc] / 다시 하기 [R] / 스테이지 선택 / 설정 / 메인으로
        /// </summary>
        public void ShowPause(string stage, string turns, Action onRetry, Action onStages, Action onSettings, Action onMain)
        {
            ClosePopups();
            GameObject blocker = null;
            const float width = 320, height = 34 + 18 + 26 + 48 + 8 + 4 * (44 + 8) - 8 + 8 + 16 + 18 + 4;
            WindowFrame(_popupRoot, "paused", null, (1280 - width) / 2, 150, width, height, true, ClosePopups, out RectTransform body, out blocker);
            Transform bar = blocker.transform.Find("Window/TitleBar");
            for (int i = 0; i < 2 && bar != null; i++) bar.Find("WindowButton" + i)?.gameObject.SetActive(false);
            float w = width * S - WinBorder * 2 - 36 * S, y = 18 * S;
            Text name = Label(body, stage, 12 * S, Muted, TextAnchor.MiddleLeft);
            At(name, 18 * S + 4 * S, y, w - 8 * S, 16 * S);
            Text left = Label(body, turns, 12 * S, Muted, TextAnchor.MiddleRight, UiKit.Mono);
            At(left, 18 * S + 4 * S, y, w - 8 * S, 16 * S);
            y += 16 * S + 10 * S;
            PrimaryButton(body, "▶ 계속하기", 24 * S, ClosePopups, out RectTransform resume);
            At(resume, 18 * S, y, w, 48 * S);
            Text resumeText = resume.GetComponentInChildren<Text>();
            resumeText.alignment = TextAnchor.MiddleLeft;
            resumeText.rectTransform.offsetMin = new Vector2(16 * S, 0);
            Kbd(resume.Find("Face"), "Esc", out RectTransform escKey);
            escKey.anchorMin = escKey.anchorMax = new Vector2(1, 0.5f);
            escKey.pivot = new Vector2(1, 0.5f);
            escKey.anchoredPosition = new Vector2(-16 * S, 0);
            escKey.sizeDelta = new Vector2(34 * S, 18 * S);
            y += 48 * S + 8 * S;
            foreach (var (label, key, action) in new (string, string, Action)[]
                     {
                         ("다시 하기", "R", onRetry), ("스테이지 선택", null, onStages), ("설정", null, onSettings), ("메인으로", null, onMain),
                     })
            {
                Action act = action;
                RectTransform row = Node(body, "Row:" + label);
                At(row, 18 * S, y, w, 44 * S);
                HoverArea(row, "Hover", Hex(0xeaf2ff, 0.08f), () => { ClosePopups(); act?.Invoke(); }, out Image area);
                Inset(area, 0, 0, 0, 0);
                Image line = Fill(row, "Line", TrackLine);
                At(line, 0, 44 * S - 1 * S, w, 1 * S);
                Text t = Label(row, label, 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
                Inset(t, 16 * S, 0, 0, 0);
                if (key != null)
                {
                    Kbd(row, key, out RectTransform k);
                    k.anchorMin = k.anchorMax = new Vector2(1, 0.5f);
                    k.pivot = new Vector2(1, 0.5f);
                    k.anchoredPosition = new Vector2(-16 * S, 0);
                    k.sizeDelta = new Vector2(20 * S, 18 * S);
                }
                y += 44 * S + 8 * S;
            }
            Text note = Label(body, "일시정지 중에는 턴과 해킹 시간이 흐르지 않아요", 12 * S, Dim, TextAnchor.MiddleCenter);
            At(note, 18 * S, y + 4 * S, w, 16 * S);
        }

        /// <summary>설정 창 (settings.exe). 게임 화면 위에 띄운다</summary>
        public void ShowSettings(SettingsWindow.Actions actions)
        {
            ClosePopups();
            SettingsWindow.Open(_popupRoot, actions);
        }

        /// <param name="path">위 작은 줄: "Layer 1 · INTERFACE · Operation 1"</param>
        /// <param name="title">스테이지 이름: "1-2 · 드라이브를 밀어라"</param>
        public void ShowHud(string path, string title, string quest)
        {
            _hud.SetActive(true);
            _hudPath.text = path;
            _hudTitle.text = title;
            _guides.Clear();
            SetQuest(quest);
            SetQuestStars(new (string, QuestState)[0]);
        }

        /// <summary>퀘스트 팝업에 보여 줄 자세한 내용</summary>
        public void SetQuest(string text) => _questDetail = text;

        /// <summary>quest.txt 아래 안내 칸에 새 안내를 더한다 (최근 2개만, 최신이 위)</summary>
        public void AddGuide(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _guides.Insert(0, text);
            if (_guides.Count > 2) _guides.RemoveAt(_guides.Count - 1);
            LayoutQuest();
        }

        /// <summary>quest.txt의 별 조건 줄 ("★2 모든 구간을 턴 안에 (불안정 0 / 허용 0)"). 달성 초록, 못 미침 주황</summary>
        public void SetQuestStars(IReadOnlyList<(string text, QuestState state)> rows)
        {
            _questStars = rows;
            LayoutQuest();
        }

        /// <summary>
        /// turns: 지금 구간의 남은 턴(0이면 주황, 넘기면 -1, -2 빨강), 칸 막대(구간 턴 중 남은 만큼), 구간 번호.
        /// limit이 null이면 ∞
        /// </summary>
        public void SetTurns(int? remaining, int? limit, int segment, int segments)
        {
            _turnsNumber.text = remaining?.ToString() ?? "∞";
            _turnsNumber.color = remaining < 0 ? Red : remaining == 0 ? Orange : Fg;
            _turnsSegment.text = $"구간 {segment}";
            _turnsFooter.text = limit == null
                ? $"구간 {segment + 1} / {segments} · 제한 없음"
                : $"구간 {segment + 1} / {segments} · 기준 {limit}";
            _turnsBar.gameObject.SetActive(GameSettings.TurnGauge);
            int cells = Mathf.Clamp(limit ?? 0, 0, 40);
            if (cells != _turnsBarCells)
            {
                _turnsBarCells = cells;
                UiKit.Clear(_turnsBar);
                float width = _turnsBar.sizeDelta.x, gap = 2 * S;
                float cellW = cells == 0 ? 0 : (width - gap * (cells - 1)) / cells;
                for (int i = 0; i < cells; i++)
                {
                    Image cell = Fill(_turnsBar, "Cell" + i, Track);
                    At(cell, i * (cellW + gap), 0, cellW, 8 * S);
                }
            }
            int filled = Mathf.Clamp(remaining ?? 0, 0, cells);
            for (int i = 0; i < _turnsBar.childCount; i++)
            {
                var image = _turnsBar.GetChild(i).GetComponent<Image>();
                if (image != null) image.color = i < filled ? Cyan : remaining < 0 ? Hex(0x3a141c) : Track;
            }
        }

        /// <summary>checkpoint: 시작(0)과 체크포인트마다 네모. 도착한 곳은 초록</summary>
        public void SetCheckpoints(IReadOnlyList<bool> reached)
        {
            UiKit.Clear(_checkpointDots);
            const float size = 12 * S, gap = 6 * S;
            for (int i = 0; i < reached.Count; i++)
            {
                Image dot = Fill(_checkpointDots, "Dot" + i, reached[i] ? Green : Track);
                At(dot, i * (size + gap), 0, size, size);
                if (!reached[i]) Border(dot.transform, Dim, 2 * S);
            }
        }

        /// <summary>collect: 데이터 파편 n / m (모두 모으면 초록, 없으면 흐리게), 열쇠 (가지고 있을 때만)</summary>
        public void SetCollect(int collected, int total, int keys)
        {
            _collectCount.text = $"{collected} / {total}";
            _collectCount.color = total == 0 ? Dim : collected >= total ? Green : Yellow;
            bool hasKeys = keys > 0;
            if (_keyRow.activeSelf != hasKeys)
            {
                _keyRow.SetActive(hasKeys);
                LayoutRight();
            }
            _keyCount.text = "×" + keys;
        }

        /// <summary>채팅 버튼에 안 읽은 메시지 + 받은 친구 요청 수</summary>
        public void SetChatCount(int count)
        {
            if (_chatLabel == null) return;
            _chatLabel.text = count > 0 ? $"채팅 <color=#ff4d5e>{count}</color>" : "채팅";
        }

        /// <summary>미니맵 그림을 바꾼다. 칸 비율을 지키며 미니맵 칸 안에 맞춘다</summary>
        public void SetMinimap(Texture2D texture)
        {
            _minimap.texture = texture;
            if (texture == null) return;
            float maxW = ColW - (PanelBorder + PanelPad) * 2, maxH = 144 * S;
            float scale = Mathf.Min(maxW / texture.width, maxH / texture.height);
            _minimap.rectTransform.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
        }

        // ---------------- 만들기 ----------------
        private void BuildHud()
        {
            _hud = new GameObject("Hud", typeof(RectTransform));
            _hud.transform.SetParent(_root, false);
            Inset(_hud.transform, 0, 0, 0, 0);
            Transform hud = _hud.transform;

            // 위 가운데 제목
            _hudPath = Label(hud, "", 12 * S, Muted, TextAnchor.MiddleCenter, UiKit.Mono);
            At(_hudPath, 320 * S, 20 * S, 640 * S, 16 * S);
            _hudTitle = Label(hud, "", 24 * S, Fg, TextAnchor.MiddleCenter, bold: true);
            At(_hudTitle, 320 * S, 40 * S, 640 * S, 28 * S);

            // 왼쪽: minimap
            _minimapPanel = HudPanel(hud, "minimap", "[M] 전체", () => OnMinimap?.Invoke());
            float mapH = PanelHead + PanelPad + 144 * S + PanelPad + PanelBorder * 2;
            At(_minimapPanel, ColLeft, ColTop, ColW, mapH);
            _minimap = new GameObject("Minimap", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _minimap.transform.SetParent(_minimapPanel, false);
            _minimap.raycastTarget = false;
            var mrt = _minimap.rectTransform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 1);
            mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.anchoredPosition = new Vector2(0, -(PanelBorder + PanelHead + PanelPad + 72 * S));
            BorderOnTop(_minimapPanel);

            // 왼쪽: quest.txt (높이는 내용에 맞춰 LayoutQuest가 정한다)
            _questPanel = HudPanel(hud, "quest.txt", "", () => OnQuest?.Invoke());
            At(_questPanel, ColLeft, ColTop + mapH + ColGap, ColW, 120 * S);
            Image guide = Fill(_questPanel, "Guide", Night);
            _guideBox = guide.rectTransform;
            Image guideBar = Fill(guide.transform, "Bar", Cyan);
            guideBar.rectTransform.anchorMin = new Vector2(0, 0);
            guideBar.rectTransform.anchorMax = new Vector2(0, 1);
            guideBar.rectTransform.offsetMin = Vector2.zero;
            guideBar.rectTransform.offsetMax = new Vector2(2 * S, 0);
            _guideText = Label(guide.transform, "", 12 * S, CyanLight, TextAnchor.UpperLeft, UiKit.Mono);
            _guideText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _guideText.lineSpacing = 1.3f;
            Inset(_guideText, 10 * S, 8 * S, 10 * S, 8 * S);

            // 오른쪽: turns
            _turnsPanel = HudPanel(hud, "turns", "", null, out _turnsSegment);
            float turnsH = PanelHead + 12 * S + 16 * S + 8 * S + 96 * S + 8 * S + 8 * S + 8 * S + 16 * S + 16 * S + PanelBorder * 2;
            At(_turnsPanel, ColRight, ColTop, ColW, turnsH);
            float y = PanelBorder + PanelHead + 12 * S;
            Text label = Label(_turnsPanel, "남은 턴", 12 * S, Muted, TextAnchor.MiddleCenter);
            At(label, 0, y, ColW, 16 * S);
            y += 16 * S + 8 * S;
            _turnsNumber = Label(_turnsPanel, "", 96 * S, Fg, TextAnchor.MiddleCenter, UiKit.Mono);
            At(_turnsNumber, 0, y, ColW, 96 * S);
            y += 96 * S + 8 * S;
            _turnsBar = Node(_turnsPanel, "Bar");
            At(_turnsBar, PanelBorder + 16 * S, y, ColW - (PanelBorder + 16 * S) * 2, 8 * S);
            y += 8 * S + 8 * S;
            _turnsFooter = Label(_turnsPanel, "", 12 * S, Muted, TextAnchor.MiddleCenter);
            At(_turnsFooter, 0, y, ColW, 16 * S);
            BorderOnTop(_turnsPanel);

            // 오른쪽: checkpoint
            _checkpointPanel = HudPanel(hud, "checkpoint", "", null);
            _checkpointDots = Node(_checkpointPanel, "Dots");
            At(_checkpointDots, PanelBorder + 16 * S, PanelBorder + PanelHead + 12 * S + 2 * S, 160 * S, 12 * S);
            Text warp = Label(_checkpointPanel, "워프", 12 * S, Muted, TextAnchor.MiddleRight);
            AtRight(warp, PanelBorder + 16 * S + 22 * S, PanelBorder + PanelHead + 12 * S, 80 * S, 16 * S);
            Kbd(_checkpointPanel, "Q", out RectTransform q);
            AtRight(q, PanelBorder + 16 * S, PanelBorder + PanelHead + 12 * S - 1 * S, 16 * S, 18 * S);
            BorderOnTop(_checkpointPanel);

            // 오른쪽: collect (데이터 파편 + 열쇠)
            _collectPanel = HudPanel(hud, "collect", "", () => OnCollect?.Invoke());
            float rowTop = PanelBorder + PanelHead + 12 * S;
            Image shard = Icon(_collectPanel, "desktop/fragment", 20 * S);
            if (shard != null) At(shard, PanelBorder + 16 * S, rowTop + 4 * S, 16 * S, 20 * S);
            Text shardLabel = Label(_collectPanel, "데이터 파편", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(shardLabel, PanelBorder + 16 * S + 24 * S, rowTop, 140 * S, 28 * S);
            _collectCount = Label(_collectPanel, "", 24 * S, Yellow, TextAnchor.MiddleRight, UiKit.Mono);
            AtRight(_collectCount, PanelBorder + 16 * S, rowTop, 120 * S, 28 * S);
            _keyRow = Node(_collectPanel, "Keys").gameObject;
            At(_keyRow.transform, 0, rowTop + 28 * S + 8 * S, ColW, 28 * S);
            var key = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            key.transform.SetParent(_keyRow.transform, false);
            key.sprite = ArtCatalog.ForObject(Data.MapObjectTypes.Key);
            key.preserveAspect = true;
            key.raycastTarget = false;
            At(key, PanelBorder + 14 * S, 2 * S, 20 * S, 24 * S);
            Text keyLabel = Label(_keyRow.transform, "열쇠", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(keyLabel, PanelBorder + 16 * S + 24 * S, 0, 140 * S, 28 * S);
            _keyCount = Label(_keyRow.transform, "", 24 * S, Yellow, TextAnchor.MiddleRight, UiKit.Mono);
            AtRight(_keyCount, PanelBorder + 16 * S, 0, 120 * S, 28 * S);
            _keyRow.SetActive(false);
            BorderOnTop(_collectPanel);

            // 오른쪽: 채팅 · 설정
            _buttonRow = Node(hud, "Buttons");
            float half = (ColW - 8 * S) / 2;
            foreach (var (text, action) in new (string, Action)[] { ("채팅", () => OnChat?.Invoke()), ("설정", () => OnSettings?.Invoke()) })
            {
                RectTransform cell = Node(_buttonRow, text);
                At(cell, text == "채팅" ? 0 : half + 8 * S, 0, half, 40 * S);
                Image bg = Fill(cell, "Bg", Window);
                Inset(bg, 0, 0, 0, 0);
                GhostButton(cell, text, 12 * S, action, out RectTransform b);
                Inset(b, 0, 0, 0, 0);
                if (text == "채팅") _chatLabel = b.GetComponentInChildren<Text>();
            }
            LayoutRight();

            BuildKeyHelp(hud);
        }

        /// <summary>오른쪽 열 높이 맞추기 (열쇠 줄이 생기면 collect가 길어진다)</summary>
        private void LayoutRight()
        {
            float y = ColTop + _turnsPanel.sizeDelta.y + ColGap;
            float checkH = PanelHead + 12 * S + 16 * S + 12 * S + PanelBorder * 2;
            At(_checkpointPanel, ColRight, y, ColW, checkH);
            y += checkH + ColGap;
            float collectH = PanelHead + 12 * S + 28 * S + (_keyRow.activeSelf ? 8 * S + 28 * S : 0) + 12 * S + PanelBorder * 2;
            At(_collectPanel, ColRight, y, ColW, collectH);
            y += collectH + ColGap;
            At(_buttonRow, ColRight, y, ColW, 40 * S);
        }

        /// <summary>quest.txt 높이 맞추기: 별 줄(긴 줄은 두 줄) + 최근 안내 칸</summary>
        private void LayoutQuest()
        {
            if (_questPanel == null) return;
            float width = ColW - (PanelBorder + PanelPad) * 2;
            float y = PanelBorder + PanelHead + PanelPad;
            for (int i = 0; i < Math.Max(_questStars.Count, _questRows.Count); i++)
            {
                if (i >= _questRows.Count)
                {
                    Text t = Label(_questPanel, "", 12 * S, Fg, TextAnchor.UpperLeft);
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.lineSpacing = 1.15f;
                    _questRows.Add(t);
                }
                Text row = _questRows[i];
                bool has = i < _questStars.Count;
                row.gameObject.SetActive(has);
                if (!has) continue;
                row.text = QuestRowText(_questStars[i].text, _questStars[i].state);
                At(row, PanelBorder + PanelPad, y, width, 16 * S);
                float h = Mathf.Max(16 * S, row.preferredHeight);
                row.rectTransform.sizeDelta = new Vector2(width, h);
                y += h + 8 * S;
            }
            if (_questStars.Count > 0) y += 4 * S;

            bool hasGuide = _guides.Count > 0;
            _guideBox.gameObject.SetActive(hasGuide);
            if (hasGuide)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _guides.Count; i++)
                {
                    if (i > 0) sb.Append('\n');
                    sb.Append(i == 0 ? $"> {_guides[i]}" : $"<color=#8b98b8>> {_guides[i]}</color>");
                }
                _guideText.text = sb.ToString();
                At(_guideBox, PanelBorder + PanelPad, y, width, 40 * S);
                float textW = width - 20 * S;
                _guideText.rectTransform.sizeDelta = new Vector2(-20 * S, -16 * S);
                var settings = _guideText.GetGenerationSettings(new Vector2(textW, 0));
                float h = _guideText.cachedTextGeneratorForLayout.GetPreferredHeight(_guideText.text, settings) / _guideText.pixelsPerUnit;
                _guideBox.sizeDelta = new Vector2(width, h + 16 * S);
                y += h + 16 * S;
            }
            else y -= 8 * S;
            y += PanelPad + PanelBorder;
            _questPanel.sizeDelta = new Vector2(ColW, y);
        }

        private static readonly Regex StarPrefix = new Regex(@"^(★\d)\s*(.*?)(\s*\(([^()]*)\))?$");

        /// <summary>"★2 모든 구간을 턴 안에 (불안정 0 / 허용 0)" -> 별 노란색, 진행 흐리게(다음 줄)</summary>
        private static string QuestRowText(string text, QuestState state)
        {
            Match m = StarPrefix.Match(text ?? "");
            if (!m.Success) return text;
            string body = m.Groups[2].Value;
            string color = state == QuestState.Done ? "#5cf08a" : state == QuestState.Warn ? "#ff9a3c" : null;
            if (color != null) body = $"<color={color}>{body}</color>";
            string progress = m.Groups[4].Success ? $"\n<color=#8b98b8>    {m.Groups[4].Value}</color>" : "";
            return $"<color=#ffd84a>{m.Groups[1].Value}</color> {body}{progress}";
        }

        /// <summary>HUD 상자: 반투명 창 + 제목줄(이름, 오른쪽 작은 글자). onClick이 있으면 상자 전체가 버튼</summary>
        private static RectTransform HudPanel(Transform parent, string title, string right, Action onClick) =>
            HudPanel(parent, title, right, onClick, out _);

        private static RectTransform HudPanel(Transform parent, string title, string right, Action onClick, out Text rightText)
        {
            RectTransform panel = Frame(parent, "Panel:" + title, Window, WindowLine, PanelBorder, 4 * S);
            if (onClick != null)
            {
                HoverArea(panel, "Click", Hex(0x22d3ee, 0.06f), onClick, out Image area);
                Inset(area, 0, 0, 0, 0);
            }
            Image head = Fill(panel, "Head", TitleBar);
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.pivot = new Vector2(0.5f, 1);
            head.rectTransform.offsetMin = new Vector2(PanelBorder, -(PanelBorder + 24 * S));
            head.rectTransform.offsetMax = new Vector2(-PanelBorder, -PanelBorder);
            Image line = Fill(panel, "HeadLine", Cyan);
            line.rectTransform.anchorMin = new Vector2(0, 1);
            line.rectTransform.anchorMax = new Vector2(1, 1);
            line.rectTransform.offsetMin = new Vector2(PanelBorder, -(PanelBorder + PanelHead));
            line.rectTransform.offsetMax = new Vector2(-PanelBorder, -(PanelBorder + 24 * S));
            Text name = Label(head.transform, title, 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            Inset(name, 8 * S, 0, 8 * S, 0);
            rightText = Label(head.transform, right, 12 * S, Muted, TextAnchor.MiddleRight);
            Inset(rightText, 8 * S, 0, 8 * S, 0);
            return panel;
        }

        /// <summary>키 모양 상자 (kbd): 짙은 바탕, 아래 그림자 줄, 1px 테두리, 고정폭 글자</summary>
        private static Text Kbd(Transform parent, string key, out RectTransform box)
        {
            box = Node(parent, "Key:" + key);
            Image bg = Fill(box, "Bg", Track);
            Inset(bg, 0, 0, 0, 0);
            Image shadow = Fill(box, "Shadow", Ink);
            shadow.rectTransform.anchorMin = new Vector2(0, 0);
            shadow.rectTransform.anchorMax = new Vector2(1, 0);
            shadow.rectTransform.offsetMin = Vector2.zero;
            shadow.rectTransform.offsetMax = new Vector2(0, 2 * S);
            Border(box, WindowLine, 1 * S);
            Text t = Label(box, key, 12 * S, Fg, TextAnchor.MiddleCenter, UiKit.Mono);
            Inset(t, 0, 0, 0, 2 * S);
            return t;
        }

        /// <summary>아래 키 안내 줄 (높이 56): [W A S D] 이동 · [E] 대화·해킹 · [Q] 워프 · [M] 전체 맵 · [R] 다시 · [Esc] 메뉴</summary>
        private static void BuildKeyHelp(Transform hud)
        {
            const float height = 56 * S;
            Image bar = Fill(hud, "KeyHelp", Hex(0x0a0716, 0.94f));
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0, height);
            Image top = Fill(bar.transform, "Line", TrackLine);
            top.rectTransform.anchorMin = new Vector2(0, 1);
            top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.offsetMin = new Vector2(0, -2 * S);
            top.rectTransform.offsetMax = Vector2.zero;

            var items = new (string key, string label)[]
            {
                ("W A S D", "이동"), ("E", "대화·해킹"), ("Q", "워프"), ("M", "전체 맵"), ("R", "다시"), ("Esc", "메뉴"),
            };
            RectTransform row = Node(bar.transform, "Row");
            var built = new List<(RectTransform key, float keyW, Text label, float labelW)>();
            float total = 0;
            foreach (var (k, l) in items)
            {
                Text keyText = Kbd(row, k, out RectTransform box);
                float keyW = keyText.preferredWidth + 12 * S;
                Text label = Label(row, l, 12 * S, Muted, TextAnchor.MiddleLeft);
                float labelW = label.preferredWidth;
                built.Add((box, keyW, label, labelW));
                total += keyW + 6 * S + labelW;
            }
            total += 24 * S * (items.Length - 1);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0, -1 * S);
            row.sizeDelta = new Vector2(total, 20 * S);
            float x = 0;
            foreach (var (key, keyW, label, labelW) in built)
            {
                At(key, x, 0, keyW, 20 * S);
                x += keyW + 6 * S;
                At(label, x, 0, labelW, 20 * S);
                x += labelW + 24 * S;
            }
        }

        // ---------------- 안내 (토스트) ----------------
        /// <summary>도감 기록 같은 알림의 테두리 색</summary>
        public static readonly Color GuidebookAccent = Hex(0xc3a6ff);

        private void BuildToast()
        {
            _toastBox = Frame(_root, "Toast", Window, Cyan, PanelBorder, 4 * S);
            _toast = _toastBox.gameObject;
            _toastText = Label(_toastBox, "", 12 * S, Fg, TextAnchor.MiddleLeft);
            _toastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _toastText.lineSpacing = 1.2f;
            Inset(_toastText, 12 * S, 8 * S, 12 * S, 8 * S);
            BorderOnTop(_toastBox);
        }

        public void Toast(string text) => Toast(text, Cyan);

        /// <summary>게임 화면 위쪽 가운데에 잠깐 뜨는 안내. 차례대로 하나씩</summary>
        public void Toast(string text, Color accent)
        {
            if (string.IsNullOrEmpty(text)) return;
            _toastQueue.Enqueue((text, accent));
            if (!_toast.activeSelf) NextToast();
        }

        private void NextToast()
        {
            if (_toastQueue.Count == 0) { _toast.SetActive(false); return; }
            var (text, accent) = _toastQueue.Dequeue();
            _toast.SetActive(true);
            _toastBox.SetAsLastSibling(); // 결과 화면의 어두운 판보다 위에
            foreach (Image edge in _toastBox.Find("Border").GetComponentsInChildren<Image>()) edge.color = accent;
            _toastText.text = text;
            // 글자 길이에 맞춰 상자 크기 (최대 너비 600 × 1.5, 넘치면 줄바꿈)
            const float maxW = 600 * S, padX = 12 * S, padY = 8 * S;
            var settings = _toastText.GetGenerationSettings(new Vector2(10000, 0));
            float natural = _toastText.cachedTextGeneratorForLayout.GetPreferredWidth(text, settings) / _toastText.pixelsPerUnit;
            float w = Mathf.Min(natural + 2 * padX + 2, maxW);
            settings = _toastText.GetGenerationSettings(new Vector2(w - 2 * padX, 0));
            float h = _toastText.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / _toastText.pixelsPerUnit;
            _toastBox.anchorMin = _toastBox.anchorMax = new Vector2(0.5f, 1);
            _toastBox.pivot = new Vector2(0.5f, 1);
            _toastBox.anchoredPosition = new Vector2(0, -(84 * S + 12 * S));
            _toastBox.sizeDelta = new Vector2(w, h + 2 * padY);
            _toastUntil = Time.unscaledTime + Mathf.Clamp(Regex.Replace(text, "<.*?>", "").Length * 0.08f, 2.5f, 6f);
        }
    }
}
