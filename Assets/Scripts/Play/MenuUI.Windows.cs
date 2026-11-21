using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 바탕화면 위에 뜨는 창 (시안 Executor_Export/02_ui_screens/Pop*): 시스템 대화 상자(종료·확인·안내), README.txt 메모장(공지),
    /// 도감 explorer.exe, mail.exe, store.exe. 창이 뜨면 작업 표시줄에 그 창 탭이 생기고, 닫으면(빨간 네모·Esc) 없어진다
    /// </summary>
    public partial class MenuUI
    {
        private const float WinBorder = Theme.WinBorder, WinTitleH = Theme.WinTitleH;

        /// <summary>
        /// 창 하나: 뒤를 막는 판(dim이면 어둡게) + 창 틀 + 제목줄(아이콘, 제목, 창 버튼 3개 중 닫기만 동작).
        /// body는 제목줄 아래 내용 자리. 시안 px(1280x720) 기준 위치·크기를 받아 1.5배한다
        /// </summary>
        private RectTransform DesktopWindow(string title, string icon, string tab, float left, float top, float width, float height,
            bool dim, out RectTransform body)
        {
            RectTransform win = WindowFrame(_popupRoot, title, icon, left, top, width, height, dim, ClosePopups, out body, out _);
            if (tab != null) SetWindowTab(tab);
            return win;
        }

        /// <summary>팝업을 모두 닫고 작업 표시줄의 창 탭도 뺀다</summary>
        public void ClosePopups()
        {
            UiKit.Clear(_popupRoot);
            if (_windowTab != null) SetWindowTab(null);
        }

        /// <summary>세로 스크롤 목록 (content에 위에서부터 At으로 넣고, 끝에 content.sizeDelta.y를 높이로)</summary>
        private static RectTransform ScrollList(Transform parent, out ScrollRect scroll)
        {
            RectTransform viewport = Node(parent, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);
            RectTransform content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            return content;
        }

        /// <summary>회로 기판 바탕 (격자)</summary>
        private static void BoardBackground(RectTransform parent, float width, float height)
        {
            var go = new GameObject("Grid", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = GridTexture();
            raw.uvRect = new Rect(0, 0, width / (32 * S), height / (32 * S));
            raw.raycastTarget = false;
            Inset(raw, 0, 0, 0, 0);
        }

        private static string ShortDate(DateTime utc)
        {
            DateTime local = utc.ToLocalTime();
            if (local.Date == DateTime.Now.Date) return local.ToString("HH:mm");
            if (local.Date == DateTime.Now.Date.AddDays(-1)) return "어제";
            return local.ToString("MM/dd");
        }

        // ================= 시스템 대화 상자 =================
        /// <summary>
        /// 시스템 창 (시안 PopExit): 뒤를 어둡게, 왼쪽 경고 아이콘(warning이면), 큰 제목 + 설명, 오른쪽 아래 버튼들.
        /// buttons: (글자, 할 일, 모양) 모양 = "ghost" / "primary" / "danger"
        /// </summary>
        private void SystemDialog(string windowTitle, string heading, string text, string sub, bool warning,
            params (string label, Action onClick, string style)[] buttons)
        {
            // 내용 높이에 맞춰 창 높이를 정한다
            const float width = 460;
            float innerW = width * S - WinBorder * 2 - 48 * S - (warning ? 48 * S + 20 * S : 0);
            Text probe = Label(_popupRoot, text ?? "", 12 * S, Muted, TextAnchor.UpperLeft);
            probe.horizontalOverflow = HorizontalWrapMode.Wrap;
            probe.lineSpacing = 1.25f;
            probe.rectTransform.sizeDelta = new Vector2(innerW, 10);
            float textH = string.IsNullOrEmpty(text) ? 0 : probe.preferredHeight;
            Destroy(probe.gameObject);
            float contentH = 28 * S + (textH > 0 ? 8 * S + textH : 0) + (sub != null ? 4 * S + 18 * S : 0);
            float heightPx = WinBorder * 2 + WinTitleH + 24 * S + Mathf.Max(warning ? 48 * S : 0, contentH) + 24 * S + 40 * S + 20 * S;
            float height = Mathf.Max(230, heightPx / S);

            DesktopWindow(windowTitle, null, null, (1280 - width) / 2, (720 - height) / 2 - 10, width, height, true, out RectTransform body);
            float x = 24 * S;
            if (warning)
            {
                Image icon = Icon(body, "desktop/warning", 48 * S);
                if (icon != null) At(icon, 24 * S, 24 * S, 48 * S, 48 * S);
                x += 48 * S + 20 * S;
            }
            float y = 24 * S;
            Text head = Label(body, heading, 24 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(head, x, y, innerW, 28 * S);
            y += 28 * S;
            if (textH > 0)
            {
                y += 8 * S;
                Text desc = Label(body, text, 12 * S, Muted, TextAnchor.UpperLeft);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                desc.lineSpacing = 1.25f;
                At(desc, x, y, innerW, textH);
                y += textH;
            }
            if (sub != null)
            {
                Text s = Label(body, sub, 12 * S, Dim, TextAnchor.MiddleLeft, UiKit.Mono);
                At(s, x, y + 4 * S, innerW, 18 * S);
            }

            // 버튼 (오른쪽 정렬)
            const float bh = 40 * S, bgap = 10 * S;
            float right = 24 * S;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                var (label, onClick, style) = buttons[i];
                Text measure = Label(body, label, 12 * S, Fg, TextAnchor.MiddleCenter, bold: true);
                float bw = Mathf.Max(120 * S, measure.preferredWidth + 32 * S);
                Destroy(measure.gameObject);
                RectTransform rt;
                if (style == "primary") PrimaryButton(body, label, 12 * S, onClick, out rt);
                else if (style == "danger") DangerButton(body, label, onClick, out rt);
                else
                {
                    GhostButton(body, label, 12 * S, onClick, out rt);
                    if (i == 0)
                    {
                        // 기본 선택(아니요): 바깥 흰 테두리
                        RectTransform ring = Node(rt, "Focus");
                        Inset(ring, -4 * S, -4 * S, -4 * S, -4 * S);
                        Border(ring, Fg, 2 * S);
                    }
                }
                rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-right, 20 * S);
                rt.sizeDelta = new Vector2(bw, bh);
                right += bw + bgap;
            }
        }

        /// <summary>빨간 채움 버튼 (예/삭제처럼 되돌리기 어려운 행동)</summary>
        private static Button DangerButton(Transform parent, string text, Action onClick, out RectTransform root)
        {
            root = Node(parent, "Danger:" + text);
            Image shadow = Fill(root, "Shadow", Ink);
            Inset(shadow, 3 * S, 3 * S, -3 * S, -3 * S);
            Image face = Fill(root, "Face", Color.white);
            Inset(face, 0, 0, 0, 0);
            Button b = Clickable(face, onClick, Red, Hex(0xff8a95));
            Text t = Label(face.transform, text, 12 * S, Ink, TextAnchor.MiddleCenter, bold: true);
            Inset(t, 0, 0, 0, 0);
            return b;
        }

        /// <summary>메인 화면 종료 (시안 PopExit + 시작 화면으로)</summary>
        public void ShowQuit(Action toTitle, Action quit)
        {
            ClosePopups();
            SystemDialog("시스템", "Executor를 종료할까요?", "진행 상황은 자동으로 저장됩니다.", "process executor.exe → exit(0)", true,
                ("아니요", ClosePopups, "ghost"),
                ("시작 화면으로", () => { ClosePopups(); toTitle(); }, "ghost"),
                ("예", () => quit(), "danger"));
        }

        /// <summary>확인 상자 (예 / 아니요)</summary>
        public void ShowConfirm(string title, string text, Action onYes)
        {
            SystemDialog("시스템", title, text, null, false, ("아니요", ClosePopups, "ghost"), ("예", () => onYes(), "primary"));
        }

        /// <summary>안내 상자 (확인)</summary>
        public void ShowMessage(string title, string text)
        {
            SystemDialog("시스템", title, text, null, false, ("확인", ClosePopups, "primary"));
        }

        // ================= README.txt (공지) =================
        /// <summary>
        /// 공지 메모장 (시안 PopNotice): 공지들을 한 문서로 이어 줄 번호와 함께. "## [분류] 제목"은 노란색, 고정 공지가 먼저
        /// </summary>
        public void ShowNoticeBoard(IReadOnlyList<Data.NoticeDetail> notices)
        {
            ClosePopups();
            DesktopWindow("README.txt — 메모장", "notice", "README.txt", 236, 72, 720, 500, false, out RectTransform body);
            float w = 720 * S - WinBorder * 2, h = 500 * S - WinBorder * 2 - WinTitleH;

            Image menu = Fill(body, "Menu", Night);
            At(menu, 0, 0, w, 28 * S);
            Image menuLine = Fill(body, "MenuLine", TrackLine);
            At(menuLine, 0, 28 * S, w, 2 * S);
            Text menuText = Label(menu.transform, "파일    편집    서식    보기    도움말", 12 * S, Muted, TextAnchor.MiddleLeft);
            Inset(menuText, 12 * S, 0, 0, 0);

            // 문서 줄
            var lines = new List<(string text, Color color, bool bold)> { ("# Executor 공지", Cyan, true), ("", Fg, false) };
            if (notices.Count == 0) lines.Add(("지금 게시 중인 공지가 없습니다.", Muted, false));
            foreach (var n in notices)
            {
                string pin = n.IsPinned ? "[고정] " : "";
                lines.Add(($"## {pin}[{n.CategoryLabel}] {n.Title}  <color=#4b5675>{n.PublishedAt.ToLocalTime():MM.dd}</color>", Yellow, false));
                foreach (string l in (n.Content ?? "").Replace("\r", "").Split('\n')) lines.Add((l, Fg, false));
                lines.Add(("", Fg, false));
            }
            if (lines.Count > 2 && lines[lines.Count - 1].text == "") lines.RemoveAt(lines.Count - 1);

            RectTransform area = Node(body, "Text");
            At(area, 0, 30 * S, w, h - 30 * S - 30 * S);
            RectTransform content = ScrollList(area, out _);
            Inset(content.parent, 12 * S, 16 * S, 12 * S, 8 * S);
            float textW = w - 24 * S - 48 * S;
            float y = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var (text, color, bold) = lines[i];
                Text num = Label(content, (i + 1).ToString(), 12 * S, Dim, TextAnchor.UpperRight, UiKit.Mono);
                At(num, 0, y, 36 * S, 24 * S);
                Text line = Label(content, text.Length == 0 ? " " : text, 12 * S, color, TextAnchor.UpperLeft, UiKit.Mono, bold);
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.lineSpacing = 1.5f;
                At(line, 48 * S, y + 3 * S, textW, 24 * S);
                float lh = Mathf.Max(24 * S, line.preferredHeight + 6 * S);
                line.rectTransform.sizeDelta = new Vector2(textW, lh);
                y += lh;
            }
            content.sizeDelta = new Vector2(0, y);

            Image foot = Fill(body, "Status", Night);
            At(foot, 0, h - 28 * S, w, 28 * S);
            Image footLine = Fill(body, "StatusLine", TrackLine);
            At(footLine, 0, h - 30 * S, w, 2 * S);
            Text left = Label(foot.transform, "읽기 전용", 12 * S, Muted, TextAnchor.MiddleLeft);
            Inset(left, 12 * S, 0, 12 * S, 0);
            Text right = Label(foot.transform, $"{lines.Count}줄 · UTF-8", 12 * S, Muted, TextAnchor.MiddleRight);
            Inset(right, 12 * S, 0, 12 * S, 0);
        }

        // ================= mail.exe =================
        private IReadOnlyList<Data.MailResponse> _mails;
        private long _selectedMail;
        private RectTransform _mailList, _mailArticle;
        private Action<Data.MailResponse> _onClaimMail;

        /// <summary>
        /// 메일함 (시안 PopMail): 왼쪽 받은 편지 목록(받지 않은 것은 하늘색 점) + 모두 받기, 오른쪽 본문과 첨부(받기).
        /// 보낸 사람 얼굴 자리는 검정 상자 (보낸 사람 기능은 보류)
        /// </summary>
        public void ShowMailbox(IReadOnlyList<Data.MailResponse> mails, long? selected, Action<Data.MailResponse> onClaim, Action onClaimAll)
        {
            ClosePopups();
            _mails = mails;
            _onClaimMail = onClaim;
            _selectedMail = selected != null && mails.Any(m => m.MailId == selected) ? selected.Value : mails.FirstOrDefault()?.MailId ?? 0;
            DesktopWindow("메일함 — mail.exe", "mail", "mail.exe", 236, 60, 820, 520, false, out RectTransform body);
            float w = 820 * S - WinBorder * 2, h = 520 * S - WinBorder * 2 - WinTitleH;
            const float navW = 290 * S;

            Image nav = Fill(body, "Inbox", Night);
            At(nav, 0, 0, navW - 2 * S, h);
            Image navLine = Fill(body, "InboxLine", TrackLine);
            At(navLine, navW - 2 * S, 0, 2 * S, h);
            int unclaimed = mails.Count(m => !m.Claimed);
            Text head = Label(nav.transform, $"받은 편지 <color=#22d3ee>{mails.Count}</color>", 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(head, 12 * S, 12 * S, 150 * S, 28 * S);
            if (unclaimed > 0)
            {
                GhostButton(nav.transform, "모두 받기", 12 * S, () => onClaimAll(), out RectTransform all);
                AtRight(all, 12 * S, 12 * S, 80 * S, 28 * S);
            }
            RectTransform listArea = Node(nav.transform, "List");
            At(listArea, 0, 52 * S, navW - 2 * S, h - 52 * S);
            _mailList = ScrollList(listArea, out _);
            Inset(_mailList.parent, 0, 0, 0, 0);

            _mailArticle = Node(body, "Article");
            At(_mailArticle, navW, 0, w - navW, h);
            DrawMailList();
            DrawMailArticle();
        }

        private void DrawMailList()
        {
            UiKit.Clear(_mailList);
            float width = 290 * S - 2 * S;
            float y = 0;
            if (_mails.Count == 0)
            {
                Text empty = Label(_mailList, "메일이 없습니다.", 12 * S, Muted, TextAnchor.MiddleCenter);
                At(empty, 0, 20 * S, width, 24 * S);
            }
            foreach (var m in _mails)
            {
                var mail = m;
                bool selected = m.MailId == _selectedMail;
                const float rowH = 10 * S + 16 * S + 2 * S + 16 * S + 10 * S;
                RectTransform row = Node(_mailList, "Mail" + m.MailId);
                At(row, 0, y, width, rowH);
                Image bg = Fill(row, "Bg", selected ? Hex(0x22d3ee, 0.14f) : Hex(0x22d3ee, 0.08f));
                Inset(bg, 0, 0, 0, 0);
                if (!selected) Clickable(bg, () => { _selectedMail = mail.MailId; DrawMailList(); DrawMailArticle(); },
                    new Color(1, 1, 1, 0), Color.white, new Color(1, 1, 1, 0.7f));
                if (selected)
                {
                    Image bar = Fill(row, "Bar", Cyan);
                    At(bar, 0, 0, 2 * S, rowH);
                }
                else
                {
                    Image line = Fill(row, "Line", TrackLine);
                    At(line, 0, rowH - 1 * S, width, 1 * S);
                }
                if (!m.Claimed)
                {
                    Image dot = Fill(row, "Unread", Cyan);
                    At(dot, 12 * S, 10 * S + 5 * S, 6 * S, 6 * S);
                }
                Text title = Label(row, m.Title, 12 * S, m.Claimed ? Muted : Fg, TextAnchor.MiddleLeft, bold: true);
                At(title, 30 * S, 10 * S, width - 30 * S - 60 * S, 16 * S);
                Text sub = Label(row, HasReward(m) ? "첨부 · " + m.RewardText : "첨부 없음", 12 * S, Muted, TextAnchor.MiddleLeft);
                At(sub, 30 * S, 10 * S + 18 * S, width - 42 * S, 16 * S);
                Text date = Label(row, ShortDate(m.CreatedAt), 12 * S, Dim, TextAnchor.MiddleRight, UiKit.Mono);
                At(date, width - 72 * S, 10 * S, 60 * S, 16 * S);
                y += rowH;
            }
            _mailList.sizeDelta = new Vector2(0, y);
        }

        private static bool HasReward(Data.MailResponse m) => m.RewardCoin > 0 || m.RewardItem != null;

        private void DrawMailArticle()
        {
            UiKit.Clear(_mailArticle);
            Data.MailResponse m = _mails.FirstOrDefault(x => x.MailId == _selectedMail);
            float w = _mailArticle.sizeDelta.x, h = _mailArticle.sizeDelta.y;
            const float padX = 24 * S, padY = 20 * S;
            float inner = w - padX * 2;
            if (m == null)
            {
                Text none = Label(_mailArticle, "메일을 고르세요.", 12 * S, Muted, TextAnchor.MiddleCenter);
                Inset(none, 0, 0, 0, 0);
                return;
            }
            // 머리: 보낸 사람 얼굴 자리(검정 상자) + 제목 + 날짜
            Image face = Fill(_mailArticle, "Sender", Hex(0x05040c));
            At(face, padX, padY, 40 * S, 40 * S);
            Border(face.transform, TrackLine, 1 * S);
            Text title = Label(_mailArticle, m.Title, 24 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(title, padX + 40 * S + 12 * S, padY - 2 * S, inner - 52 * S, 28 * S);
            string expiry = m.ExpiresAt == null ? "무기한" : $"{m.ExpiresAt.Value.ToLocalTime():yyyy.MM.dd HH:mm}까지";
            Text meta = Label(_mailArticle, $"{m.CreatedAt.ToLocalTime():yyyy.MM.dd HH:mm} · {expiry}", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(meta, padX + 40 * S + 12 * S, padY + 28 * S, inner - 52 * S, 16 * S);
            float y = padY + 44 * S + 14 * S;
            Image sep = Fill(_mailArticle, "Line", TrackLine);
            At(sep, padX, y, inner, 2 * S);
            y += 2 * S + 14 * S;

            // 본문
            Text content = Label(_mailArticle, string.IsNullOrEmpty(m.Content) ? "(내용 없음)" : m.Content, 12 * S,
                string.IsNullOrEmpty(m.Content) ? Muted : Fg, TextAnchor.UpperLeft);
            content.horizontalOverflow = HorizontalWrapMode.Wrap;
            content.verticalOverflow = VerticalWrapMode.Truncate;
            content.lineSpacing = 1.6f;
            float attachH = HasReward(m) ? 12 * S + 36 * S + 12 * S : 0;
            At(content, padX, y, inner, h - y - padY - (attachH > 0 ? attachH + 14 * S : 0));

            // 첨부
            if (attachH > 0)
            {
                Image box = Fill(_mailArticle, "Attachment", Night);
                At(box, padX, h - padY - attachH, inner, attachH);
                Border(box.transform, TrackLine, 2 * S);
                RectTransform coin = Node(box.transform, "Coin");
                At(coin, 12 * S, 12 * S + 6 * S, 24 * S, 24 * S);
                Image dark = Fill(coin, "Dark", YellowDark);
                Inset(dark, 0, 0, 0, 0);
                Image yellow = Fill(coin, "Face", Yellow);
                Inset(yellow, 0, 0, 3 * S, 3 * S);
                Text label = Label(box.transform, $"첨부 · <color=#ffd84a>{m.RewardText}</color>", 12 * S, Fg, TextAnchor.MiddleLeft);
                Inset(label, 12 * S + 24 * S + 12 * S, 0, 120 * S, 0);
                RectTransform btn;
                if (m.Claimed)
                {
                    Button done = GhostButton(box.transform, "받음", 12 * S, null, out btn);
                    done.interactable = false;
                }
                else PrimaryButton(box.transform, "받기", 12 * S, () => _onClaimMail?.Invoke(m), out btn);
                AtRight(btn, 12 * S, 12 * S, 84 * S, 36 * S);
            }
        }

        // ================= store.exe =================
        /// <summary>
        /// 상점 (시안 PopStore): 종류 탭 + 보유 코인, 3열 카드(미리 보기·이름·가격·구매/보유 중/코인 부족)
        /// </summary>
        public void ShowShop(IReadOnlyList<Data.ShopItem> items, int coin, string filter, Action<string> onFilter,
            Action<Data.ShopItem> onBuy)
        {
            ClosePopups();
            DesktopWindow("store.exe", "shop", "store.exe", 236, 60, 860, 540, false, out RectTransform body);
            float w = 860 * S - WinBorder * 2, h = 540 * S - WinBorder * 2 - WinTitleH;

            // 탭 줄
            const float tabH = 44 * S;
            Image bar = Fill(body, "Tabs", Night);
            At(bar, 0, 0, w, tabH);
            Image barLine = Fill(body, "TabsLine", TrackLine);
            At(barLine, 0, tabH, w, 2 * S);
            float tx = 12 * S;
            foreach (var (type, label) in Data.ItemTypes.Tabs)
            {
                string t = type;
                bool active = t == filter;
                Text tl = Label(bar.transform, label, 12 * S, active ? Fg : Muted, TextAnchor.MiddleCenter, bold: true);
                float tw = tl.preferredWidth + 28 * S;
                RectTransform tab = Node(bar.transform, "Tab:" + label);
                At(tab, tx, 0, tw, tabH);
                tl.transform.SetParent(tab, false);
                Inset(tl, 0, 0, 0, 0);
                if (active)
                {
                    Image underline = Fill(tab, "Line", Cyan);
                    At(underline, 0, tabH - 2 * S, tw, 2 * S);
                }
                else
                {
                    HoverArea(tab, "Hover", Hex(0x22d3ee, 0.10f), () => onFilter(t), out Image area);
                    Inset(area, 0, 0, 0, 0);
                    area.transform.SetAsFirstSibling();
                }
                tx += tw + 4 * S;
            }
            RectTransform chip = Node(bar.transform, "Coin");
            Text coinText = Label(chip, coin.ToString(), 12 * S, Yellow, TextAnchor.MiddleLeft, UiKit.Mono);
            float chipW = 12 * S + 12 * S + 8 * S + coinText.preferredWidth + 12 * S;
            AtRight(chip, 12 * S, (tabH - 28 * S) / 2, chipW, 28 * S);
            Image chipBg = Fill(chip, "Bg", Ink);
            Inset(chipBg, 0, 0, 0, 0);
            chipBg.transform.SetAsFirstSibling();
            CoinIcon(chip, 12 * S, (28 * S - 12 * S) / 2, 12 * S);
            Inset(coinText, 12 * S + 12 * S + 8 * S, 0, 0, 0);

            // 카드 판
            RectTransform board = Node(body, "Board");
            At(board, 0, tabH + 2 * S, w, h - tabH - 2 * S);
            BoardBackground(board, w, h - tabH - 2 * S);
            RectTransform content = ScrollList(board, out _);
            Inset(content.parent, 16 * S, 16 * S, 16 * S, 0);
            if (items.Count == 0)
            {
                Text empty = Label(content, "판매 중인 아이템이 없습니다.", 12 * S, Muted, TextAnchor.MiddleCenter);
                At(empty, 0, 40 * S, w - 32 * S, 24 * S);
                return;
            }
            const float gap = 14 * S, cardH = 12 * S + 72 * S + 10 * S + 16 * S + 10 * S + 36 * S + 12 * S;
            float cardW = (w - 32 * S - gap * 2) / 3;
            for (int i = 0; i < items.Count; i++)
            {
                Data.ShopItem item = items[i];
                RectTransform card = Node(content, "Item" + item.ItemId);
                At(card, (i % 3) * (cardW + gap), (i / 3) * (cardH + gap), cardW, cardH);
                Image bg = Fill(card, "Bg", Night);
                Inset(bg, 0, 0, 0, 0);
                // 미리 보기: 아이템 그림이 있으면 그것, 없으면 색 테두리 터미널
                Image preview = Fill(card, "Preview", Ink);
                At(preview, 12 * S, 12 * S, cardW - 24 * S, 72 * S);
                Sprite art = item.ItemType == Data.ItemTypes.Cursor || item.ItemType == Data.ItemTypes.Delta ? null : ArtCatalog.Item(item.Image);
                if (item.ItemType == Data.ItemTypes.Cursor)
                {
                    // 커서: 터미널 프롬프트 뒤에 그 모양으로 깜박이는 커서
                    CursorStyle style = ParseCursor(item.Image);
                    RectTransform term = Node(preview.transform, "Terminal");
                    term.anchorMin = term.anchorMax = new Vector2(0.5f, 0.5f);
                    term.sizeDelta = new Vector2(96 * S, 40 * S);
                    Border(term, TrackLine, 2 * S);
                    Text prompt = Label(term, "$", 12 * S, Muted, TextAnchor.MiddleLeft, UiKit.Mono);
                    Inset(prompt, 8 * S, 0, 0, 0);
                    Image cur = Fill(term, "Cursor", style.Color);
                    cur.rectTransform.anchorMin = cur.rectTransform.anchorMax = new Vector2(0, 0.5f);
                    cur.rectTransform.pivot = new Vector2(0, 0);
                    cur.rectTransform.sizeDelta = style.Size;
                    cur.rectTransform.anchoredPosition = new Vector2(24 * S, -7 * S);
                    UiAnimator.Blink(cur, style.Period);
                }
                else if (item.ItemType == Data.ItemTypes.Delta)
                {
                    // 델타 꾸미기: 꾸민 얼굴
                    RectTransform face = Node(preview.transform, "Delta");
                    face.anchorMin = face.anchorMax = new Vector2(0.5f, 0.5f);
                    face.pivot = new Vector2(0.5f, 0.5f);
                    face.sizeDelta = new Vector2(40 * S, 40 * S);
                    face.anchoredPosition = new Vector2(0, -4 * S);
                    DrawDelta(face, 40 * S, item.Image);
                }
                else if (art != null)
                {
                    Image img = Fill(preview.transform, "Art", Color.white);
                    img.sprite = art;
                    img.preserveAspect = true;
                    Inset(img, 6 * S, 6 * S, 6 * S, 6 * S);
                }
                else
                {
                    Color accent = PreviewColor(item);
                    RectTransform term = Node(preview.transform, "Terminal");
                    term.anchorMin = term.anchorMax = new Vector2(0.5f, 0.5f);
                    term.sizeDelta = new Vector2(96 * S, 40 * S);
                    Border(term, accent, 2 * S);
                    Text prompt = Label(term, ">_", 12 * S, accent, TextAnchor.MiddleLeft, UiKit.Mono);
                    Inset(prompt, 8 * S, 0, 0, 0);
                }
                Text name = Label(card, $"{Data.ItemTypes.Label(item.ItemType)} · {item.Name}", 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
                At(name, 12 * S, 12 * S + 72 * S + 10 * S, cardW - 24 * S, 16 * S);
                float by = 12 * S + 72 * S + 10 * S + 16 * S + 10 * S;
                bool poor = !item.Owned && item.Price > coin;
                if (!item.Owned)
                {
                    RectTransform price = Node(card, "Price");
                    At(price, 12 * S, by, cardW / 2, 36 * S);
                    CoinIcon(price, 0, (36 * S - 10 * S) / 2, 10 * S);
                    Text p = Label(price, item.Price.ToString(), 12 * S, poor ? Red : Yellow, TextAnchor.MiddleLeft, UiKit.Mono);
                    Inset(p, 16 * S, 0, 0, 0);
                }
                RectTransform btn;
                if (item.Owned || poor)
                {
                    Button b = GhostButton(card, item.Owned ? "보유 중" : "코인 부족", 12 * S, null, out btn);
                    b.interactable = false;
                    foreach (Image edge in btn.Find("Border").GetComponentsInChildren<Image>()) edge.color = TrackLine;
                    btn.GetComponentInChildren<Text>().color = Dim;
                }
                else PrimaryButton(card, "구매", 12 * S, () => onBuy(item), out btn);
                AtRight(btn, 12 * S, by, item.Owned || poor ? 90 * S : 64 * S, 36 * S);
                Border(card, item.Owned ? Cyan : TrackLine, 2 * S);
            }
            int rows = (items.Count + 2) / 3;
            content.sizeDelta = new Vector2(0, rows * cardH + (rows - 1) * gap + 16 * S);
        }

        /// <summary>그림 없는 아이템의 미리 보기 색 (이름에 색 단어가 있으면 그 색, 아니면 종류 색)</summary>
        private static Color PreviewColor(Data.ShopItem item)
        {
            string n = (item.Name ?? "") + (item.Description ?? "");
            if (n.Contains("앰버") || n.Contains("amber")) return Hex(0xffb23e);
            if (n.Contains("그린") || n.Contains("green")) return Green;
            if (n.Contains("핑크") || n.Contains("언더")) return Hex(0xff4fa3);
            if (n.Contains("보라") || n.Contains("퍼플")) return Hex(0xc3a6ff);
            return item.ItemType == Data.ItemTypes.Theme ? Cyan : Orange;
        }

        private static void CoinIcon(Transform parent, float left, float top, float size)
        {
            RectTransform coin = Node(parent, "CoinIcon");
            At(coin, left, top, size, size);
            Image dark = Fill(coin, "Dark", YellowDark);
            Inset(dark, 0, 0, 0, 0);
            Image face = Fill(coin, "Face", Yellow);
            Inset(face, 0, 0, size / 6, size / 6);
        }

        // ================= 도감 explorer.exe =================
        private RectTransform _guideAside, _guideGrid;
        private Data.GuidebookListResponse _guideList;
        private long _selectedGuide;
        private Action<long> _onGuideSelect;

        /// <summary>
        /// 도감 (시안 PopGuide): 주소칸 + 수집 n / m, 왼쪽 4열 칸(모르는 것은 ???), 오른쪽 상세(SetGuidebookDetail로 채움)
        /// </summary>
        public void ShowGuidebook(Data.GuidebookListResponse list, long? selected, Action<long> onSelect)
        {
            ClosePopups();
            _guideList = list;
            _onGuideSelect = onSelect;
            _selectedGuide = selected ?? list.Items.FirstOrDefault(i => i.Unlocked)?.GuidebookId ?? 0;
            DesktopWindow("도감 — explorer.exe", "folder", "도감", 236, 60, 860, 540, false, out RectTransform body);
            float w = 860 * S - WinBorder * 2, h = 540 * S - WinBorder * 2 - WinTitleH;

            const float toolH = 44 * S, asideW = 300 * S;
            Image tool = Fill(body, "Toolbar", Night);
            At(tool, 0, 0, w, toolH);
            Image toolLine = Fill(body, "ToolbarLine", TrackLine);
            At(toolLine, 0, toolH, w, 2 * S);
            Text count = Label(tool.transform, $"수집 <color=#c3a6ff>{list.UnlockedCount} / {list.TotalCount}</color>", 12 * S, Muted, TextAnchor.MiddleRight);
            AtRight(count, 12 * S, 0, 140 * S, toolH);
            Image path = Fill(tool.transform, "Path", Ink);
            At(path, 12 * S, (toolH - 28 * S) / 2, w - 24 * S - 140 * S - 8 * S, 28 * S);
            Border(path.transform, TrackLine, 2 * S);
            Text pathText = Label(path.transform, "C:\\Executor\\<color=#eaf2ff>도감</color>", 12 * S, Muted, TextAnchor.MiddleLeft, UiKit.Mono);
            Inset(pathText, 10 * S, 0, 10 * S, 0);

            RectTransform board = Node(body, "Board");
            At(board, 0, toolH + 2 * S, w - asideW, h - toolH - 2 * S);
            BoardBackground(board, w - asideW, h - toolH - 2 * S);
            _guideGrid = ScrollList(board, out _);
            Inset(_guideGrid.parent, 16 * S, 16 * S, 16 * S, 0);

            Image asideLine = Fill(body, "AsideLine", TrackLine);
            At(asideLine, w - asideW, toolH + 2 * S, 2 * S, h - toolH - 2 * S);
            Image aside = Fill(body, "Aside", Night);
            At(aside, w - asideW + 2 * S, toolH + 2 * S, asideW - 2 * S, h - toolH - 2 * S);
            _guideAside = aside.rectTransform;
            DrawGuideGrid();
            if (_selectedGuide != 0) onSelect(_selectedGuide);
            else
            {
                Text none = Label(_guideAside, list.Items.Count == 0 ? "아직 등록된 도감 항목이 없습니다." : "스테이지에서 이상 개체를 만나면\n여기에 기록됩니다.",
                    12 * S, Muted, TextAnchor.MiddleCenter);
                none.horizontalOverflow = HorizontalWrapMode.Wrap;
                Inset(none, 18 * S, 0, 18 * S, 0);
            }
        }

        private void DrawGuideGrid()
        {
            UiKit.Clear(_guideGrid);
            float width = 860 * S - WinBorder * 2 - 300 * S - 32 * S;
            const float gap = 12 * S, cardH = 104 * S;
            float cardW = (width - gap * 3) / 4;
            var items = _guideList.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                bool selected = it.GuidebookId == _selectedGuide;
                RectTransform card = Node(_guideGrid, "Card" + it.GuidebookId);
                At(card, (i % 4) * (cardW + gap), (i / 4) * (cardH + gap), cardW, cardH);
                Image bg = Fill(card, "Bg", selected ? Hex(0x22d3ee, 0.14f) : new Color(0, 0, 0, 0));
                Inset(bg, 0, 0, 0, 0);
                if (it.Unlocked && !selected)
                {
                    long id = it.GuidebookId;
                    Clickable(bg, () => { _selectedGuide = id; DrawGuideGrid(); _onGuideSelect?.Invoke(id); },
                        Color.white, Hex(0x22d3ee, 0.10f), Hex(0x22d3ee, 0.14f));
                    bg.color = Color.white;
                    var colors = bg.GetComponent<Button>().colors;
                    colors.normalColor = new Color(0, 0, 0, 0);
                    bg.GetComponent<Button>().colors = colors;
                }
                Border(card, selected ? Cyan : it.Unlocked ? WindowLine : TrackLine, 2 * S);
                Sprite art = it.Unlocked ? ArtCatalog.Guidebook(it.Image) : null;
                Image icon;
                if (art != null)
                {
                    icon = Fill(card, "Art", Color.white);
                    icon.sprite = art;
                    icon.preserveAspect = true;
                }
                else icon = Icon(card, it.Unlocked ? "desktop/entity" : "desktop/unknown", 40 * S);
                if (icon != null) At(icon, (cardW - 40 * S) / 2, 14 * S, 40 * S, 40 * S);
                Text name = Label(card, it.Unlocked ? it.Title : "???", 12 * S, it.Unlocked ? Fg : Dim, TextAnchor.UpperCenter);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                At(name, 6 * S, 14 * S + 40 * S + 8 * S, cardW - 12 * S, 36 * S);
            }
            int rows = (items.Count + 3) / 4;
            _guideGrid.sizeDelta = new Vector2(0, rows * cardH + Math.Max(0, rows - 1) * gap + 16 * S);
        }

        /// <summary>도감 오른쪽 상세: 그림, 분류 · 언어, 이름, 설명, 코드 예시, 대처, 처음 만난 곳, 맵에서</summary>
        public void SetGuidebookDetail(Data.GuidebookDetail d, string stageLabel, Sprite image)
        {
            if (_guideAside == null || d.GuidebookId != _selectedGuide) return;
            UiKit.Clear(_guideAside);
            const float pad = 18 * S;
            float width = 300 * S - 2 * S - pad * 2;
            float y = pad;

            Image icon;
            if (image != null)
            {
                icon = Fill(_guideAside, "Art", Color.white);
                icon.sprite = image;
                icon.preserveAspect = true;
            }
            else icon = Icon(_guideAside, "desktop/entity", 56 * S);
            if (icon != null) At(icon, pad, y, 56 * S, 56 * S);
            string kind = Data.GuidebookCategories.Label(d.Category) + (string.IsNullOrEmpty(d.Language) ? "" : " · " + d.Language);
            Text cat = Label(_guideAside, kind, 12 * S, Hex(0xc3a6ff), TextAnchor.MiddleLeft);
            At(cat, pad + 56 * S + 12 * S, y + 8 * S, width - 68 * S, 16 * S);
            Text title = Label(_guideAside, d.Title, 12 * S, Fg, TextAnchor.UpperLeft, bold: true);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            At(title, pad + 56 * S + 12 * S, y + 26 * S, width - 68 * S, 30 * S);
            y += 56 * S + 12 * S;

            Text Para(string text, Color color, Font font = null)
            {
                Text t = Label(_guideAside, text, 12 * S, color, TextAnchor.UpperLeft, font);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.lineSpacing = 1.4f;
                At(t, pad, y, width, 20 * S);
                float th = t.preferredHeight;
                t.rectTransform.sizeDelta = new Vector2(width, th);
                y += th + 12 * S;
                return t;
            }
            if (!string.IsNullOrEmpty(d.Comment)) Para(d.Comment, Fg);
            if (!string.IsNullOrEmpty(d.Code))
            {
                Image codeBox = Fill(_guideAside, "Code", Ink);
                Text code = Label(codeBox.transform, d.Code.Replace("\r", ""), 12 * S, Fg, TextAnchor.UpperLeft, UiKit.Mono);
                code.horizontalOverflow = HorizontalWrapMode.Wrap;
                code.lineSpacing = 1.3f;
                code.rectTransform.sizeDelta = new Vector2(width - 24 * S, 10);
                float ch = code.preferredHeight;
                At(codeBox, pad, y, width, ch + 20 * S);
                Inset(code, 12 * S, 10 * S, 12 * S, 10 * S);
                y += ch + 20 * S + 12 * S;
            }
            if (!string.IsNullOrEmpty(d.Remedy)) Para("대처: " + d.Remedy, Muted);

            // 아래: 처음 만난 곳 / 맵에서
            var rows = new List<(string, string)>();
            if (!string.IsNullOrEmpty(stageLabel)) rows.Add(("처음 만난 곳", stageLabel));
            if (!string.IsNullOrEmpty(d.MapNote)) rows.Add(("맵에서", d.MapNote));
            float bottom = _guideAside.sizeDelta.y - pad;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                Text l = Label(_guideAside, rows[i].Item1, 12 * S, Muted, TextAnchor.MiddleLeft);
                At(l, pad, bottom - 16 * S, 84 * S, 16 * S);
                Text v = Label(_guideAside, rows[i].Item2, 12 * S, Fg, TextAnchor.MiddleLeft);
                At(v, pad + 84 * S, bottom - 16 * S, width - 84 * S, 16 * S);
                bottom -= 16 * S + 6 * S;
            }
        }
    }
}
