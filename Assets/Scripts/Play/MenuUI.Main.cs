using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 메인 화면 (시안 Executor_Export/02_ui_screens/Main). 컴퓨터 바탕화면처럼:
    /// 왼쪽 바탕화면 아이콘(공지 README.txt·도감·메일·store.exe·친구), 오른쪽 위 델타 알림, 가운데 로고와 executor.exe 터미널 창
    /// (진행 상황 로그, 구역 진행 막대, ▶ 이어하기 / 스테이지 선택), 아래 작업 표시줄(시작 화면·프로필·코인·설정·시계·종료).
    /// 시안 px × 1.5 = 화면 px
    /// </summary>
    public partial class MenuUI
    {
        /// <summary>메인 화면에 보여 줄 내용</summary>
        public class MainInfo
        {
            public string Nickname;
            /// <summary>코인 칸 글자 ("180", 오프라인이면 "오프라인")</summary>
            public string Coin;
            /// <summary>지금 구역(레이어) 이름. 예: INTERFACE</summary>
            public string Zone;
            public int Cleared, Total;
            /// <summary>별 합계 (서버 기록이 있을 때만). 없으면 Stars &lt; 0</summary>
            public int Stars = -1, MaxStars;
            /// <summary>마지막으로 클리어한 스테이지 "2. 드라이브를 밀어라" (없으면 null)와 그 별</summary>
            public string LastStage;
            public int LastStars = -1, LastMaxStars;
        }

        public Action OnProfile, OnMail, OnShop, OnNotice, OnGuidebook, OnFriends, OnStages, OnContinue, OnQuit, OnMainTitle;

        private Text[] _mainLog;
        private Text _mainZone, _mainZoneCount, _deltaText;
        private RectTransform _mainZoneBar, _mainZoneFill, _mainCursor, _deltaFace;
        private string _cursorKey, _deltaKey;
        // 바탕화면·작업 표시줄은 메인과 스테이지 선택 화면에 하나씩 있다
        private readonly List<Text> _mailBadges = new List<Text>();
        private readonly List<Text> _friendBadges = new List<Text>();
        private readonly List<Text> _taskNames = new List<Text>();
        private readonly List<Text> _taskCoins = new List<Text>();
        private int _mailCount, _friendCount;

        /// <summary>작업 표시줄의 닉네임·코인 (모든 화면)</summary>
        public void SetPlayer(string nickname, string coin)
        {
            foreach (Text t in _taskNames) t.text = string.IsNullOrEmpty(nickname) ? "플레이어" : nickname;
            foreach (Text t in _taskCoins) t.text = coin;
        }

        public void ShowMain(MainInfo info)
        {
            HideAll();
            _main.SetActive(true);
            string name = string.IsNullOrEmpty(info.Nickname) ? "플레이어" : info.Nickname;
            SetPlayer(name, info.Coin);
            _deltaText.text = $"…반환을 멈출 수 없습니다.\n<color=#8b98b8>{name}, 여기까지 와 주세요.</color>";

            string zone = string.IsNullOrEmpty(info.Zone) ? "EXECUTOR" : info.Zone;
            int bugs = Math.Max(0, info.Total - info.Cleared);
            string last = info.LastStage == null
                ? "<color=#22d3ee>[INFO]</color> 아직 클리어한 스테이지가 없습니다"
                : $"<color=#22d3ee>[INFO]</color> 마지막 기록 · {info.LastStage} {StarText(info.LastStars, info.LastMaxStars)}";
            _mainLog[0].text = "<color=#8b98b8>$ ./executor --resume</color>";
            _mainLog[1].text = $"<color=#5cf08a>[ OK ]</color> 사용자 확인 · {name}";
            _mainLog[2].text = bugs > 0
                ? $"<color=#ff4d5e>[WARN]</color> {zone} 구역에서 버그 반응 {bugs}건"
                : $"<color=#5cf08a>[ OK ]</color> {zone} 구역의 버그를 모두 제거했습니다";
            _mainLog[3].text = last;
            _mainLog[4].text = "<color=#8b98b8>$ </color>";

            _mainZone.text = zone;
            _mainZoneCount.text = info.Stars >= 0
                ? $"{info.Cleared}/{info.Total} · <color=#ffd84a>★{info.Stars}</color>/{info.MaxStars}"
                : $"{info.Cleared}/{info.Total}";
            LayoutZoneRow(info.Total > 0 ? (float)info.Cleared / info.Total : 0);
            PlaceCursor();
        }

        /// <summary>장착한 커서·델타 꾸미기 (아이템 image 키, 없으면 null). 메인 화면에 바로 반영한다</summary>
        public void SetCosmetics(string cursorKey, string deltaKey)
        {
            _cursorKey = cursorKey;
            _deltaKey = deltaKey;
            if (_mainCursor != null && _mainLog != null) PlaceCursor();
            if (_deltaFace != null) DrawDelta(_deltaFace, _deltaFace.sizeDelta.x, deltaKey);
        }

        public struct CursorStyle
        {
            public Vector2 Size;
            public Color Color;
            public float Period;
        }

        /// <summary>
        /// 커서 모양 키: block · underline · bar, 뒤에 -slow(2초) / -fast(0.5초)와 :#rrggbb(색). 모르는 키나 null은 기본(하늘색 블록, 1초)
        /// </summary>
        public static CursorStyle ParseCursor(string key)
        {
            var style = new CursorStyle { Size = new Vector2(8 * S, 14 * S), Color = Cyan, Period = 1f };
            if (string.IsNullOrWhiteSpace(key)) return style;
            string k = key.Trim().ToLowerInvariant();
            int colon = k.IndexOf(':');
            if (colon >= 0)
            {
                if (ColorUtility.TryParseHtmlString(k.Substring(colon + 1).Trim(), out Color c)) style.Color = c;
                k = k.Substring(0, colon);
            }
            if (k.EndsWith("-slow")) { style.Period = 2f; k = k.Substring(0, k.Length - 5); }
            else if (k.EndsWith("-fast")) { style.Period = 0.5f; k = k.Substring(0, k.Length - 5); }
            if (k == "underline") style.Size = new Vector2(8 * S, 2 * S);
            else if (k == "bar") style.Size = new Vector2(2 * S, 14 * S);
            return style;
        }

        /// <summary>
        /// 델타 얼굴(10x10 도트)을 size 상자에 그리고, 꾸미기(Resources/UI/delta/{키}, 12x12 도트: 얼굴은 (1,2)부터)를 겹친다
        /// </summary>
        private static void DrawDelta(RectTransform box, float size, string deltaKey)
        {
            UiKit.Clear(box);
            Image face = Icon(box, "desktop/delta", size);
            if (face != null) At(face, 0, 0, size, size);
            if (string.IsNullOrWhiteSpace(deltaKey)) return;
            Sprite overlay = ArtCatalog.Ui("delta/" + deltaKey.Trim().ToLowerInvariant());
            if (overlay == null) return;
            float px = size / 10f;
            Image o = Fill(box, "Decoration", Color.white);
            o.sprite = overlay;
            At(o, -px, -2 * px, 12 * px, 12 * px);
        }

        private static string StarText(int stars, int max)
        {
            if (stars < 0 || max <= 0) return "";
            return $"<color=#ffd84a>{new string('★', stars)}</color><color=#4b5675>{new string('★', Math.Max(0, max - stars))}</color>";
        }

        private GameObject BuildMain()
        {
            var screen = Fill(_root, "Main", Night, raycast: true);
            Inset(screen, 0, 0, 0, 0);
            // 배경: 흐린 학교 사진 + 어둡게 + 균열·글리치 (시안을 그대로 그린 그림)
            Sprite wallpaper = ArtCatalog.Illustration("main_desktop");
            if (wallpaper != null) Cover(screen.rectTransform, wallpaper);

            BuildDesktopIcons(screen.transform);
            BuildDeltaNotice(screen.transform);
            BuildLogo(screen.transform);
            BuildTerminal(screen.transform);
            BuildTaskbar(screen.transform, new[] { ("executor.exe", (Action)null) });
            return screen.gameObject;
        }

        // ---------------- 바탕화면 아이콘 ----------------
        private void BuildDesktopIcons(Transform screen)
        {
            var items = new (string icon, string label, Action action)[]
            {
                ("notice", "README.txt", () => OnNotice?.Invoke()),
                ("guidebook", "도감", () => OnGuidebook?.Invoke()),
                ("mail", "메일함", () => OnMail?.Invoke()),
                ("shop", "store.exe", () => OnShop?.Invoke()),
                ("friends", "친구", () => OnFriends?.Invoke()),
            };
            const float width = 96 * S, height = 86 * S, gap = 12 * S;
            for (int i = 0; i < items.Length; i++)
            {
                var (icon, label, action) = items[i];
                RectTransform cell = Node(screen, "Desktop:" + label);
                At(cell, 24 * S, 24 * S + i * (height + gap), width, height);
                // 마우스를 올리면 옅은 하늘색 바탕
                HoverArea(cell, "Hover", Hex(0x22d3ee, 0.18f), action, out Image area);
                Inset(area, 0, 0, 0, 0);
                Image image = Icon(cell, "desktop/" + icon, 48 * S);
                if (image != null)
                {
                    image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 1);
                    image.rectTransform.pivot = new Vector2(0.5f, 1);
                    image.rectTransform.anchoredPosition = new Vector2(0, -8 * S);
                }
                Text text = Label(cell, label, 12 * S, Fg, TextAnchor.UpperCenter);
                At(text, 0, (8 + 48 + 6) * S, width, 16 * S);
                Outlined(text);
                Text badge = Badge(cell);
                if (icon == "mail") _mailBadges.Add(badge);
                if (icon == "friends") _friendBadges.Add(badge);
            }
        }

        /// <summary>아이콘 오른쪽 위 빨간 숫자 (0이면 숨김)</summary>
        private static Text Badge(Transform cell)
        {
            RectTransform root = Node(cell, "Badge");
            AtRight(root, 18 * S - 2 * S, 4 * S - 2 * S, 24 * S, 24 * S);
            Image outline = Fill(root, "Outline", Ink);
            Inset(outline, 0, 0, 0, 0);
            Image face = Fill(root, "Face", Red);
            Inset(face, 2 * S, 2 * S, 2 * S, 2 * S);
            Text text = Label(face.transform, "", 12 * S, Ink, TextAnchor.MiddleCenter, bold: true);
            Inset(text, 0, 0, 0, 0);
            root.gameObject.SetActive(false);
            return text;
        }

        private static void SetBadge(Text badge, int count)
        {
            if (badge == null) return;
            var root = (RectTransform)badge.transform.parent.parent;
            root.gameObject.SetActive(count > 0);
            badge.text = count > 99 ? "99+" : count.ToString();
            // 숫자가 길면 왼쪽으로 늘린다 (오른쪽 끝은 고정)
            root.sizeDelta = new Vector2(Mathf.Max(24 * S, badge.preferredWidth + 12 * S), 24 * S);
        }

        /// <summary>바탕화면 친구 아이콘에 안 읽은 메시지 + 받은 친구 요청 수 (0이면 숨김)</summary>
        public void SetFriendCount(int count)
        {
            _friendCount = count;
            foreach (Text b in _friendBadges) SetBadge(b, count);
        }

        /// <summary>바탕화면 메일함 아이콘에 받지 않은 메일 수 (0이면 숨김)</summary>
        public void SetMailCount(int unclaimed)
        {
            _mailCount = unclaimed;
            foreach (Text b in _mailBadges) SetBadge(b, unclaimed);
        }

        // ---------------- 델타 알림 ----------------
        private void BuildDeltaNotice(Transform screen)
        {
            const float width = 320 * S, border = 2 * S, header = 28 * S, pad = 12 * S, icon = 40 * S;
            float height = border + header + pad + icon + pad + border;
            RectTransform box = Frame(screen, "DeltaNotice", Hex(0x0c0a18, 0.94f), Orange, border, 4 * S);
            AtRight(box, 24 * S, 24 * S, width, height);

            Image bar = Fill(box, "Header", Orange);
            At(bar, border, border, width - border * 2, header);
            Text title = Label(bar.transform, "델타 · 응답 생성 중", 12 * S, Ink, TextAnchor.MiddleLeft, bold: true);
            Inset(title, 10 * S, 0, 10 * S, 0);
            Text meter = Label(bar.transform, "▮▮▮▯", 12 * S, Ink, TextAnchor.MiddleRight, UiKit.Mono);
            Inset(meter, 10 * S, 0, 10 * S, 0);

            _deltaFace = Node(box, "DeltaFace");
            At(_deltaFace, border + pad, border + header + pad, icon, icon);
            DrawDelta(_deltaFace, icon, _deltaKey);
            _deltaText = Label(box, "", 12 * S, Fg, TextAnchor.UpperLeft);
            _deltaText.lineSpacing = 1.2f;
            At(_deltaText, border + pad + icon + pad, border + header + pad, width - (border + pad) * 2 - icon - pad, icon);
            BorderOnTop(box);
        }

        // ---------------- 로고 ----------------
        private static void BuildLogo(Transform screen)
        {
            RectTransform logo = Node(screen, "Logo");
            At(logo, 560 * S, 112 * S, 640 * S, 52 * S);
            // 글자 겹치기: 자홍(오른쪽) + 하늘(왼쪽) + 흰색 (text-shadow 3px 0 #d61ad0, -3px 0 #22d3ee)
            foreach (var (color, dx) in new[] { (Magenta, 3 * S), (Cyan, -3 * S), (Fg, 0f) })
            {
                Text t = Label(logo, "EXECUTOR", 48 * S, color, TextAnchor.LowerLeft, bold: true);
                Inset(t, dx, 0, -dx, 0);
            }
            Text sub = Label(logo, "명령을 실행하고, 버그를 제거하라.", 12 * S, CyanLight, TextAnchor.LowerRight);
            Inset(sub, 0, 0, 0, 6 * S);
        }

        // ---------------- executor.exe 터미널 창 ----------------
        private const float TermWidth = 640 * S, TermBorder = 2 * S, TermPadX = 24 * S;

        private void BuildTerminal(Transform screen)
        {
            const float titleH = 32 * S + 2 * S, padTop = 20 * S, lineH = 22 * S, gap = 20 * S, rowH = 16 * S, buttonH = 56 * S, padBottom = 24 * S;
            float top = TermBorder + titleH + padTop;
            float zoneTop = top + lineH * 5 + gap;
            float buttonTop = zoneTop + rowH + gap;
            float height = buttonTop + buttonH + padBottom + TermBorder;
            RectTransform win = Frame(screen, "Terminal", Window, WindowLine, TermBorder, 6 * S);
            At(win, 560 * S, 186 * S, TermWidth, height);
            WindowTitle(win, "executor.exe — bash", TermWidth, TermBorder);

            // 로그 (고정폭 글자, 줄마다 22 간격)
            _mainLog = new Text[5];
            for (int i = 0; i < _mainLog.Length; i++)
            {
                _mainLog[i] = Label(win, "", 12 * S, Fg, TextAnchor.MiddleLeft, UiKit.Mono);
                At(_mainLog[i], TermBorder + TermPadX, top + lineH * i, TermWidth - (TermBorder + TermPadX) * 2, lineH);
            }
            Image cursor = Fill(win, "Cursor", Cyan);
            _mainCursor = cursor.rectTransform;
            UiAnimator.Blink(cursor);

            // 구역 진행 막대: INTERFACE ■■■■□□□ 6/12 · ★14/36
            _mainZone = Label(win, "", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(_mainZone, TermBorder + TermPadX, zoneTop, 200 * S, rowH);
            _mainZoneCount = Label(win, "", 12 * S, Fg, TextAnchor.MiddleRight);
            At(_mainZoneCount, TermWidth - TermBorder - TermPadX - 200 * S, zoneTop, 200 * S, rowH);
            Image track = Fill(win, "ZoneTrack", Track);
            _mainZoneBar = track.rectTransform;
            Border(track.transform, TrackLine, 1 * S);
            Image fill = Fill(track.transform, "ZoneFill", Cyan);
            _mainZoneFill = fill.rectTransform;
            _mainZoneFill.anchorMin = new Vector2(0, 0);
            _mainZoneFill.anchorMax = new Vector2(0, 1);
            _mainZoneFill.pivot = new Vector2(0, 0.5f);
            _mainZoneFill.offsetMin = _mainZoneFill.offsetMax = Vector2.zero;
            _mainZoneBar.anchorMin = _mainZoneBar.anchorMax = new Vector2(0, 1);
            _mainZoneBar.pivot = new Vector2(0, 0.5f);
            _mainZoneBar.anchoredPosition = new Vector2(0, -(zoneTop + rowH / 2));

            // 버튼: ▶ 이어하기 (주) / 스테이지 선택
            float inner = TermWidth - (TermBorder + TermPadX) * 2;
            const float ghostW = 200 * S, between = 12 * S;
            PrimaryButton(win, "▶ 이어하기", 24 * S, () => OnContinue?.Invoke(), out RectTransform cont);
            At(cont, TermBorder + TermPadX, buttonTop, inner - ghostW - between, buttonH);
            GhostButton(win, "스테이지 선택", 12 * S, () => OnStages?.Invoke(), out RectTransform select);
            At(select, TermBorder + TermPadX + inner - ghostW, buttonTop, ghostW, buttonH);
            BorderOnTop(win);
        }

        /// <summary>구역 진행 막대를 글자 길이에 맞춰 놓는다 (이름 | 12px | 막대 | 12px | 숫자)</summary>
        private void LayoutZoneRow(float ratio)
        {
            float left = TermBorder + TermPadX + _mainZone.preferredWidth + 12 * S;
            float right = TermWidth - TermBorder - TermPadX - _mainZoneCount.preferredWidth - 12 * S;
            _mainZoneBar.anchoredPosition = new Vector2(left, _mainZoneBar.anchoredPosition.y);
            _mainZoneBar.sizeDelta = new Vector2(Mathf.Max(0, right - left), 10 * S);
            _mainZoneFill.sizeDelta = new Vector2(Mathf.Max(0, right - left) * Mathf.Clamp01(ratio), 0);
        }

        /// <summary>깜박이는 커서를 마지막 줄 "$ " 바로 뒤에 둔다</summary>
        private void PlaceCursor()
        {
            Text line = _mainLog[4];
            RectTransform log = line.rectTransform;
            CursorStyle style = ParseCursor(_cursorKey);
            _mainCursor.anchorMin = _mainCursor.anchorMax = new Vector2(0, 1);
            _mainCursor.pivot = new Vector2(0, 0);
            _mainCursor.sizeDelta = style.Size;
            // 커서 아래쪽을 글자 아래쪽(블록 높이 14 기준)에 맞춘다
            _mainCursor.anchoredPosition = new Vector2(log.anchoredPosition.x + line.preferredWidth + 2 * S,
                log.anchoredPosition.y - log.sizeDelta.y / 2 - 7 * S);
            _mainCursor.GetComponent<Image>().color = style.Color;
            UiAnimator.Blink(_mainCursor.GetComponent<Image>(), style.Period);
        }

        /// <summary>창 제목 줄: 제목(굵게) + 최소화·최대화·닫기 모양(장식) + 아래 하늘색 줄</summary>
        private static void WindowTitle(RectTransform win, string title, float width, float border)
        {
            const float height = 32 * S;
            Image bar = Fill(win, "TitleBar", TitleBar);
            At(bar, border, border, width - border * 2, height);
            Image line = Fill(win, "TitleLine", Cyan);
            At(line, border, border + height, width - border * 2, 2 * S);
            Text text = Label(bar.transform, title, 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            Inset(text, 12 * S, 0, 0, 0);
            const float box = 16 * S, gap = 6 * S;
            for (int i = 0; i < 3; i++)
            {
                RectTransform b = Node(bar.transform, "WindowButton" + i);
                RectTransform rt = b;
                rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
                rt.pivot = new Vector2(1, 0.5f);
                rt.anchoredPosition = new Vector2(-(8 * S + (2 - i) * (box + gap)), 0);
                rt.sizeDelta = new Vector2(box, box);
                Image face = Fill(b, "Face", i == 2 ? Hex(0xa3203a) : TrackLine);
                Inset(face, 0, 0, 0, 0);
                if (i == 0)
                {
                    Image underline = Fill(b, "Min", Muted);
                    underline.rectTransform.anchorMin = new Vector2(0, 0);
                    underline.rectTransform.anchorMax = new Vector2(1, 0);
                    underline.rectTransform.offsetMin = Vector2.zero;
                    underline.rectTransform.offsetMax = new Vector2(0, 3 * S);
                }
                else Border(b, i == 2 ? Red : Muted, 2 * S);
            }
        }

        // ---------------- 작업 표시줄 ----------------
        /// <summary>
        /// 화면 아래 작업 표시줄 (높이 48). 왼쪽: EXECUTOR(시작 화면) | 프로필 | 코인, 가운데: 열린 창 이름들, 오른쪽: 설정 · 시계 · 종료.
        /// windows: (이름, 누르면 할 일). 할 일이 null인 것이 지금 창(강조)
        /// </summary>
        private void BuildTaskbar(Transform screen, (string name, Action open)[] windows)
        {
            const float height = 48 * S, item = 40 * S;
            Image bar = Fill(screen, "Taskbar", Window, raycast: true);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0, height);
            Image topLine = Fill(bar.transform, "TopLine", TrackLine);
            topLine.rectTransform.anchorMin = new Vector2(0, 1);
            topLine.rectTransform.anchorMax = new Vector2(1, 1);
            topLine.rectTransform.offsetMin = new Vector2(0, -2 * S);
            topLine.rectTransform.offsetMax = Vector2.zero;

            Color hover = Hex(0xeaf2ff, 0.08f);
            float x = 8 * S;

            // EXECUTOR: 시작 화면으로
            float startW = 12 * S + 20 * S + 8 * S + 72 * S + 12 * S;
            RectTransform start = TaskItem(bar.transform, x, startW, item, hover, () => OnMainTitle?.Invoke());
            Image logo = Icon(start, "desktop/start", 20 * S);
            if (logo != null) At(logo, 12 * S, (item - 20 * S) / 2, 20 * S, 20 * S);
            Text startText = Label(start, "EXECUTOR", 12 * S, Cyan, TextAnchor.MiddleLeft, bold: true);
            Inset(startText, 12 * S + 20 * S + 8 * S, 0, 0, 0);
            x += startW + 4 * S;

            Image sep = Fill(bar.transform, "Separator", TrackLine);
            At(sep, x + 4 * S, (height - 24 * S) / 2 + 1 * S, 2 * S, 24 * S);
            x += 2 * S + 8 * S + 4 * S;

            // 프로필: 아바타 + 이름
            float profileW = 220 * S;
            RectTransform profile = TaskItem(bar.transform, x, profileW, item, hover, () => OnProfile?.Invoke());
            RectTransform avatar = Node(profile, "Avatar");
            At(avatar, 12 * S, (item - 24 * S) / 2, 24 * S, 24 * S);
            Image head = Fill(avatar, "Fill", Hex(0x2f2a2c));
            Inset(head, 0, 0, 0, 0);
            Image body = Fill(avatar, "Body", Hex(0xaeaaa8));
            body.rectTransform.anchorMin = new Vector2(0, 0);
            body.rectTransform.anchorMax = new Vector2(1, 0);
            body.rectTransform.offsetMin = Vector2.zero;
            body.rectTransform.offsetMax = new Vector2(0, 8 * S);
            Border(avatar, Dim, 2 * S);
            Text taskName = Label(profile, "", 12 * S, Fg, TextAnchor.MiddleLeft);
            Inset(taskName, 12 * S + 24 * S + 8 * S, 0, 8 * S, 0);
            _taskNames.Add(taskName);
            x += profileW + 4 * S;

            // 코인
            RectTransform coin = Node(bar.transform, "Coin");
            At(coin, x, (height - item) / 2 + 1 * S, 160 * S, item);
            RectTransform chip = Node(coin, "CoinIcon");
            At(chip, 12 * S, (item - 12 * S) / 2, 12 * S, 12 * S);
            Image chipDark = Fill(chip, "Dark", YellowDark);
            Inset(chipDark, 0, 0, 0, 0);
            Image chipFace = Fill(chip, "Face", Yellow);
            Inset(chipFace, 0, 0, 2 * S, 2 * S);
            Text taskCoin = Label(coin, "", 12 * S, Yellow, TextAnchor.MiddleLeft);
            Inset(taskCoin, 12 * S + 12 * S + 6 * S, 0, 0, 0);
            _taskCoins.Add(taskCoin);

            // 가운데: 열린 창들
            RectTransform tabRow = Node(bar.transform, "Tabs");
            tabRow.anchorMin = tabRow.anchorMax = new Vector2(0.5f, 0);
            tabRow.pivot = new Vector2(0.5f, 0);
            tabRow.anchoredPosition = new Vector2(0, (height - item) / 2 - 1 * S);
            tabRow.sizeDelta = new Vector2(0, item);
            _taskbars.Add((tabRow, windows));
            DrawTaskbarTabs(tabRow, windows, _windowTab);

            // 오른쪽: 설정 · 시계 · 종료
            float right = 8 * S;
            RectTransform power = TaskItemRight(bar.transform, right, item, item, Hex(0xff4d5e, 0.35f), () => OnQuit?.Invoke());
            Image powerIcon = Icon(power, "desktop/power", 20 * S);
            if (powerIcon != null) At(powerIcon, (item - 20 * S) / 2, (item - 20 * S) / 2, 20 * S, 20 * S);
            right += item + 4 * S;
            Text clock = Label(bar.transform, "", 12 * S, Muted, TextAnchor.MiddleCenter, UiKit.Mono);
            AtRight(clock, right, (height - item) / 2 + 1 * S, 64 * S, item);
            UiAnimator.Clock(clock);
            right += 64 * S + 4 * S;
            RectTransform settings = TaskItemRight(bar.transform, right, item, item, hover, () => OnSettings?.Invoke());
            Image gear = Icon(settings, "desktop/settings", 20 * S);
            if (gear != null) At(gear, (item - 20 * S) / 2, (item - 20 * S) / 2, 20 * S, 20 * S);
        }

        /// <summary>작업 표시줄마다 (탭 줄, 기본 창 목록)</summary>
        private readonly List<(RectTransform row, (string name, Action open)[] windows)> _taskbars =
            new List<(RectTransform, (string, Action)[])>();
        /// <summary>지금 떠 있는 팝업 창의 탭 이름 (README.txt, mail.exe ...). 없으면 null</summary>
        private string _windowTab;

        /// <summary>팝업 창이 뜨거나 닫히면 작업 표시줄에 그 창 탭을 더하거나 뺀다</summary>
        public void SetWindowTab(string name)
        {
            _windowTab = name;
            foreach (var (row, windows) in _taskbars) DrawTaskbarTabs(row, windows, name);
        }

        /// <summary>
        /// 탭 그리기: 지금 창은 옅은 하늘색 + 아래 줄, 다른 창은 흐린 글자로 누르면 그 화면으로.
        /// 팝업 창(extra)이 떠 있으면 그것이 지금 창이고, 화면 창을 누르면 팝업을 닫는다
        /// </summary>
        private void DrawTaskbarTabs(RectTransform row, (string name, Action open)[] windows, string extra)
        {
            UiKit.Clear(row);
            var list = new List<(string name, Action open)>();
            foreach (var w in windows) list.Add(extra != null && w.open == null ? (w.name, (Action)ClosePopups) : w);
            if (extra != null) list.Add((extra, null));
            const float item = 40 * S;
            Color hover = Hex(0xeaf2ff, 0.08f);
            float x = 0;
            foreach (var (name, open) in list)
            {
                bool current = open == null;
                Image tab = Fill(row, "Tab:" + name, current ? Hex(0x22d3ee, 0.10f) : hover);
                Text tabText = Label(tab.transform, name, 12 * S, current ? Fg : Muted, TextAnchor.MiddleCenter);
                float w = tabText.preferredWidth + 32 * S;
                Inset(tabText, 0, 0, 0, 0);
                if (current)
                {
                    Image line = Fill(tab.transform, "Line", Cyan);
                    line.rectTransform.anchorMin = new Vector2(0, 0);
                    line.rectTransform.anchorMax = new Vector2(1, 0);
                    line.rectTransform.offsetMin = Vector2.zero;
                    line.rectTransform.offsetMax = new Vector2(0, 2 * S);
                }
                else Clickable(tab, open, new Color(1, 1, 1, 0), Color.white, new Color(1, 1, 1, 0.7f));
                At(tab, x, 0, w, item);
                x += w + 4 * S;
            }
            row.sizeDelta = new Vector2(Mathf.Max(0, x - 4 * S), item);
        }

        private static RectTransform TaskItem(Transform bar, float x, float width, float height, Color hover, Action onClick)
        {
            RectTransform node = Node(bar, "TaskItem");
            At(node, x, (48 * S - height) / 2 + 1 * S, width, height);
            HoverArea(node, "Hover", hover, onClick, out Image area);
            Inset(area, 0, 0, 0, 0);
            return node;
        }

        private static RectTransform TaskItemRight(Transform bar, float right, float width, float height, Color hover, Action onClick)
        {
            RectTransform node = Node(bar, "TaskItem");
            AtRight(node, right, (48 * S - height) / 2 + 1 * S, width, height);
            HoverArea(node, "Hover", hover, onClick, out Image area);
            Inset(area, 0, 0, 0, 0);
            return node;
        }
    }
}
