using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 공지 창 (시안 Executor/notice_ui, README.txt — 공지). 메모장 모양.
    /// 왼쪽: 검색 + 분류 탭(전체·이벤트·업데이트·안내) + 공지 목록(안 읽음 점·굵게, 3일 안의 안 읽은 공지는 NEW) + "공지 N개 · 안 읽음 M".
    /// 오른쪽: 파일 경로 줄, 지난 공지면 노란 안내 띠(최신 공지 보기 →), 줄 번호가 붙은 본문(# 제목 · &gt; 날짜 · ## 소제목 · `강조` · ---),
    /// 아래 [← 최신 쪽] "i / N · 읽기 전용 · UTF-8" [지난 쪽 →]. 읽음 표시는 이 PC에 계정별로 저장한다.
    /// </summary>
    public partial class MenuUI
    {
        private static readonly Color EventPink = Hex(0xff4fa3), CodeOrange = Hex(0xff9a3c);

        private List<Data.NoticeDetail> _notices = new List<Data.NoticeDetail>();
        private string _noticeFilter;   // null = 전체, "EVENT", "UPDATE", "INFO"(공지·점검)
        private string _noticeQuery = "";
        private long _noticeSelected;
        private RectTransform _noticeList, _noticeArticle, _noticeTabs;
        private Text _noticeCount;

        private static string ReadKey => "notice_read_" + PlayerSession.ProgressPrefix;

        private static HashSet<long> ReadIds()
        {
            string saved = PlayerPrefs.GetString(ReadKey, "");
            return new HashSet<long>(saved.Split(',').Where(s => long.TryParse(s, out _)).Select(long.Parse));
        }

        private static void MarkRead(long id)
        {
            HashSet<long> ids = ReadIds();
            if (!ids.Add(id)) return;
            PlayerPrefs.SetString(ReadKey, string.Join(",", ids.OrderByDescending(i => i).Take(200)));
            PlayerPrefs.Save();
        }

        private static (string label, Color color) NoticeKind(string category)
        {
            switch (category)
            {
                case "EVENT": return ("이벤트", EventPink);
                case "UPDATE": return ("업데이트", Cyan);
                case "MAINTENANCE": return ("점검", Green);
                default: return ("안내", Green);
            }
        }

        private static string FilterOf(string category) =>
            category == "EVENT" || category == "UPDATE" ? category : "INFO";

        private List<Data.NoticeDetail> VisibleNotices()
        {
            string q = _noticeQuery.Trim();
            return _notices.Where(n => (_noticeFilter == null || FilterOf(n.Category) == _noticeFilter)
                                       && (q.Length == 0 || (n.Title ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                                                         || (n.Content ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        public void ShowNoticeBoard(IReadOnlyList<Data.NoticeDetail> notices)
        {
            ClosePopups();
            // 고정 공지 먼저, 그다음 최신순
            _notices = notices.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.PublishedAt).ToList();
            _noticeFilter = null;
            _noticeQuery = "";
            _noticeSelected = _notices.Count > 0 ? _notices[0].NoticeId : 0;
            if (_noticeSelected != 0) MarkRead(_noticeSelected);

            const float width = 900, height = 580;
            DesktopWindow("README.txt — 공지", "notice", "README.txt", 190, 40, width, height, false, out RectTransform body);
            float W = width * S - WinBorder * 2, H = height * S - WinBorder * 2 - WinTitleH;

            // 메뉴 줄 (모양만)
            Image menu = Fill(body, "Menu", Night);
            At(menu, 0, 0, W, 28 * S);
            Image menuLine = Fill(body, "MenuLine", TrackLine);
            At(menuLine, 0, 28 * S, W, 2 * S);
            Text menuText = Label(menu.transform, "파일    편집    보기    도움말", 12 * S, Muted, TextAnchor.MiddleLeft);
            Inset(menuText, 12 * S, 0, 0, 0);
            float top = 30 * S, h = H - top;

            // 왼쪽: 검색 · 탭 · 목록 · 개수
            const float navW = 270 * S;
            Image nav = Fill(body, "Nav", Night);
            At(nav, 0, top, navW, h);
            Image navLine = Fill(body, "NavLine", TrackLine);
            At(navLine, navW - 2 * S, top, 2 * S, h);
            InputField search = UiKit.Input(nav.transform, "공지 검색", limit: 40, size: Mathf.RoundToInt(12 * S));
            At(search, 10 * S, 10 * S, navW - 22 * S, 28 * S);
            FieldFrame.Apply(search);
            if (search.GetComponent<Image>() is Image sb) sb.color = Ink;
            foreach (Text t in new[] { search.textComponent, search.placeholder as Text })
            {
                if (t == null) continue;
                t.font = UiKit.Regular;
                t.rectTransform.offsetMin = new Vector2(8 * S, 0);
                t.rectTransform.offsetMax = new Vector2(-8 * S, 0);
            }
            search.onValueChanged.AddListener(v => { _noticeQuery = v; DrawNoticeList(); });
            _noticeTabs = Node(nav.transform, "Tabs");
            At(_noticeTabs, 10 * S, 46 * S, navW - 20 * S, 24 * S);
            Image searchLine = Fill(nav.transform, "SearchLine", TrackLine);
            At(searchLine, 0, 80 * S, navW - 2 * S, 2 * S);
            _noticeList = Node(nav.transform, "List");
            At(_noticeList, 0, 82 * S, navW - 2 * S, h - 82 * S - 34 * S);
            Image countLine = Fill(nav.transform, "CountLine", TrackLine);
            At(countLine, 0, h - 34 * S, navW - 2 * S, 2 * S);
            _noticeCount = Label(nav.transform, "", 12 * S, Dim, TextAnchor.MiddleLeft);
            At(_noticeCount, 10 * S, h - 32 * S, navW - 20 * S, 32 * S);

            // 오른쪽: 본문
            _noticeArticle = Node(body, "Article");
            At(_noticeArticle, navW, top, W - navW, h);

            DrawNoticeTabs();
            DrawNoticeList();
            DrawNoticeArticle();
        }

        private void DrawNoticeTabs()
        {
            if (_noticeTabs == null) return;
            UiKit.Clear(_noticeTabs);
            float x = 0;
            foreach (var (filter, label, color) in new (string, string, Color)[]
                     { (null, "전체", Cyan), ("EVENT", "이벤트", EventPink), ("UPDATE", "업데이트", Cyan), ("INFO", "안내", Green) })
            {
                bool on = filter == _noticeFilter;
                float w = TextW(_noticeTabs, label, 12 * S, true) + 16 * S;
                RectTransform tab = Node(_noticeTabs, "Tab:" + label);
                At(tab, x, 0, w, 24 * S);
                Image face = Fill(tab, "Face", on ? Cyan : new Color(0, 0, 0, 0), raycast: !on);
                Inset(face, 0, 0, 0, 0);
                if (!on)
                {
                    string pick = filter;
                    Clickable(face, () => { _noticeFilter = pick; DrawNoticeTabs(); DrawNoticeList(); }, new Color(0, 0, 0, 0), Hex(0xeaf2ff, 0.08f));
                    Border(tab, TrackLine, 1 * S);
                }
                Text t = Label(tab, label, 12 * S, on ? Ink : color, TextAnchor.MiddleCenter, bold: true);
                Inset(t, 0, 0, 0, 0);
                x += w + 4 * S;
            }
        }

        private void DrawNoticeList()
        {
            if (_noticeList == null) return;
            UiKit.Clear(_noticeList);
            HashSet<long> read = ReadIds();
            List<Data.NoticeDetail> list = VisibleNotices();
            int unread = _notices.Count(n => !read.Contains(n.NoticeId));
            _noticeCount.text = $"공지 {_notices.Count}개 · 안 읽음 {unread}";

            RectTransform content = ScrollList(_noticeList, out _);
            Inset(content.parent, 0, 0, 0, 0);
            float w = _noticeList.rect.width > 0 ? _noticeList.rect.width : 268 * S;
            const float rowH = 52 * S;
            if (list.Count == 0)
            {
                Text empty = Label(content, _notices.Count == 0 ? "게시 중인 공지가 없어요." : "찾는 공지가 없어요.", 12 * S, Dim, TextAnchor.MiddleCenter);
                At(empty, 0, 16 * S, w, 16 * S);
                content.sizeDelta = new Vector2(0, 48 * S);
                return;
            }
            float y = 0;
            foreach (Data.NoticeDetail n in list)
            {
                bool selected = n.NoticeId == _noticeSelected, isRead = read.Contains(n.NoticeId);
                bool isNew = !isRead && (DateTime.UtcNow - Utc(n.PublishedAt)).TotalDays <= 3;
                var (kind, color) = NoticeKind(n.Category);
                RectTransform row = Node(content, "Notice" + n.NoticeId);
                At(row, 0, y, w, rowH);
                Image face = Fill(row, "Face", selected ? Hex(0x22d3ee, 0.14f) : new Color(0, 0, 0, 0), raycast: true);
                Inset(face, 0, 0, 0, 0);
                long id = n.NoticeId;
                Clickable(face, () => SelectNotice(id), selected ? Hex(0x22d3ee, 0.14f) : new Color(0, 0, 0, 0), Hex(0x22d3ee, 0.10f));
                if (selected)
                {
                    Image bar = Fill(row, "Selected", Cyan);
                    At(bar, 0, 0, 2 * S, rowH);
                }
                else
                {
                    Image sep = Fill(row, "Line", TrackLine);
                    At(sep, 0, rowH - 1 * S, w, 1 * S);
                }
                if (!isRead)
                {
                    Image dot = Fill(row, "Unread", Cyan);
                    At(dot, 10 * S, 9 * S + 5 * S, 6 * S, 6 * S);
                }
                string pin = n.IsPinned ? "<color=#ffd84a>[고정]</color> " : "";
                string tag = isNew ? "  <color=#0c0a18><b> NEW </b></color>" : "";
                Text head = Label(row, $"{pin}<color=#{ColorUtility.ToHtmlStringRGB(color)}>[{kind}]</color>{tag}", 12 * S, Fg, TextAnchor.MiddleLeft);
                At(head, 26 * S, 9 * S, w - 26 * S - 56 * S, 16 * S);
                if (isNew)
                {
                    // NEW 바탕 (글자 뒤 빨간 칸)
                    float before = TextW(row, $"{pin}[{kind}]  ", 12 * S, false);
                    Image badge = Fill(row, "NewBack", Red);
                    At(badge, 26 * S + before, 9 * S, TextW(row, " NEW ", 12 * S, true), 16 * S);
                    badge.transform.SetSiblingIndex(head.transform.GetSiblingIndex());
                }
                Text title = Label(row, n.Title ?? "", 12 * S, isRead ? Muted : Fg, TextAnchor.MiddleLeft, bold: !isRead);
                title.horizontalOverflow = HorizontalWrapMode.Wrap;
                title.verticalOverflow = VerticalWrapMode.Truncate;
                At(title, 26 * S, 9 * S + 18 * S, w - 26 * S - 10 * S, 16 * S);
                Text date = Label(row, Utc(n.PublishedAt).ToLocalTime().ToString("MM.dd"), 12 * S, Dim, TextAnchor.MiddleRight, UiKit.Mono);
                At(date, w - 10 * S - 50 * S, 9 * S, 50 * S, 16 * S);
                y += rowH;
            }
            content.sizeDelta = new Vector2(0, y);
        }

        private void SelectNotice(long id)
        {
            _noticeSelected = id;
            MarkRead(id);
            DrawNoticeList();
            DrawNoticeArticle();
        }

        private void DrawNoticeArticle()
        {
            if (_noticeArticle == null) return;
            UiKit.Clear(_noticeArticle);
            float w = _noticeArticle.rect.width > 0 ? _noticeArticle.rect.width : 626 * S;
            float h = _noticeArticle.rect.height > 0 ? _noticeArticle.rect.height : 516 * S;
            List<Data.NoticeDetail> list = VisibleNotices();
            Data.NoticeDetail n = _notices.FirstOrDefault(x => x.NoticeId == _noticeSelected);
            if (n == null)
            {
                Text empty = Label(_noticeArticle, "읽을 공지를 왼쪽에서 골라 주세요.", 12 * S, Dim, TextAnchor.MiddleCenter);
                Inset(empty, 0, 0, 0, 0);
                return;
            }
            DateTime local = Utc(n.PublishedAt).ToLocalTime();

            // 파일 경로 줄
            Image pathBar = Fill(_noticeArticle, "Path", Ink);
            At(pathBar, 0, 0, w, 30 * S);
            Image pathLine = Fill(_noticeArticle, "PathLine", TrackLine);
            At(pathLine, 0, 28 * S, w, 2 * S);
            Text path = Label(pathBar.transform, $"<color=#8b98b8>C:\\Executor\\notice\\</color>{local:yyyy-MM-dd}_{(n.Category ?? "notice").ToLowerInvariant()}.md",
                12 * S, Fg, TextAnchor.MiddleLeft, UiKit.Mono);
            Inset(path, 12 * S, 0, 12 * S, 2 * S);
            float y = 30 * S;

            // 지난 공지 안내 띠 (가장 최근 공지가 아니면)
            Data.NoticeDetail latest = _notices.OrderByDescending(x => x.PublishedAt).First();
            if (latest.NoticeId != n.NoticeId)
            {
                Image band = Fill(_noticeArticle, "Old", Hex(0xffd84a, 0.08f));
                At(band, 0, y, w, 32 * S);
                Image bandLine = Fill(_noticeArticle, "OldLine", YellowDark);
                At(bandLine, 0, y + 31 * S, w, 1 * S);
                Text old = Label(band.transform, $"지난 공지예요 · {local:MM.dd}", 12 * S, Yellow, TextAnchor.MiddleLeft);
                Inset(old, 12 * S, 0, 160 * S, 0);
                RectTransform go = Node(band.transform, "Latest");
                AtRight(go, 8 * S, 4 * S, 130 * S, 24 * S);
                long latestId = latest.NoticeId;
                HoverArea(go, "Hover", Hex(0x22d3ee, 0.10f), () => { _noticeFilter = null; _noticeQuery = ""; DrawNoticeTabs(); SelectNotice(latestId); }, out Image area);
                Inset(area, 0, 0, 0, 0);
                Text goText = Label(go, "최신 공지 보기 →", 12 * S, Cyan, TextAnchor.MiddleRight);
                Inset(goText, 0, 0, 4 * S, 0);
                y += 32 * S;
            }

            // 본문 (줄 번호 + 간단한 마크다운 색)
            const float footH = 36 * S;
            RectTransform textArea = Node(_noticeArticle, "Text");
            At(textArea, 0, y, w, h - y - footH);
            RectTransform content = ScrollList(textArea, out _);
            Inset(content.parent, 12 * S, 14 * S, 12 * S, 6 * S);
            float lineW = w - 24 * S - 34 * S;
            var lines = new List<string>
            {
                "# " + (n.Title ?? ""),
                $"> {local:yyyy-MM-dd} · {(n.Category == "UPDATE" ? "개발" : "운영자")}",
                "",
            };
            lines.AddRange((n.Content ?? "").Replace("\r", "").Split('\n'));
            float ly = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                Text num = Label(content, (i + 1).ToString(), 12 * S, Dim, TextAnchor.UpperRight, UiKit.Mono);
                Text body = Label(content, Markdown(lines[i]), 12 * S, Fg, TextAnchor.UpperLeft, UiKit.Mono);
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.lineSpacing = 1.35f;
                At(body, 34 * S, ly + 3 * S, lineW, 16 * S);
                float lh = Mathf.Max(22 * S, body.preferredHeight + 6 * S);
                At(body, 34 * S, ly + 3 * S, lineW, lh);
                At(num, 0, ly + 3 * S, 22 * S, 16 * S);
                ly += lh;
            }
            content.sizeDelta = new Vector2(0, ly);

            // 아래: 최신 쪽 · i / N · 지난 쪽 (지금 목록 기준)
            Image foot = Fill(_noticeArticle, "Foot", Night);
            At(foot, 0, h - footH, w, footH);
            Image footLine = Fill(_noticeArticle, "FootLine", TrackLine);
            At(footLine, 0, h - footH, w, 2 * S);
            int index = list.FindIndex(x => x.NoticeId == n.NoticeId);
            int total = list.Count;
            Text pos = Label(foot.transform, index >= 0 ? $"{index + 1} / {total} · 읽기 전용 · UTF-8" : "읽기 전용 · UTF-8", 12 * S, Muted, TextAnchor.MiddleCenter);
            Inset(pos, 0, 2 * S, 0, 0);
            PageButton(foot.transform, "← 최신 쪽", true, index > 0 ? list[index - 1].NoticeId : (long?)null);
            PageButton(foot.transform, "지난 쪽 →", false, index >= 0 && index + 1 < total ? list[index + 1].NoticeId : (long?)null);
        }

        private void PageButton(Transform foot, string label, bool left, long? target)
        {
            RectTransform b = Node(foot, label);
            float bw = 96 * S;
            if (left) At(b, 12 * S, 6 * S, bw, 28 * S);
            else AtRight(b, 12 * S, 6 * S, bw, 28 * S);
            if (target != null)
            {
                long id = target.Value;
                HoverArea(b, "Hover", Hex(0x22d3ee, 0.10f), () => SelectNotice(id), out Image area);
                Inset(area, 0, 0, 0, 0);
            }
            Text t = Label(b, label, 12 * S, target != null ? Cyan : Dim, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, bold: true);
            Inset(t, 10 * S, 0, 10 * S, 0);
        }

        /// <summary>한 줄 마크다운 → 색: # 제목(하늘·굵게), ##·### 소제목(노랑), &gt; 인용(회색), ---(구분선), `강조`(주황)</summary>
        private static string Markdown(string line)
        {
            if (line.Length == 0) return " ";
            string t = line.TrimEnd();
            if (Regex.IsMatch(t, @"^\s*(-{3,}|─{3,}|={3,})\s*$")) return "<color=#4b5675>────────────────────────────</color>";
            if (t.StartsWith("# ")) return $"<color=#22d3ee><b>{Inline(t)}</b></color>";
            if (t.StartsWith("## ") || t.StartsWith("### ")) return $"<color=#ffd84a>{Inline(t)}</color>";
            if (t.StartsWith(">")) return $"<color=#8b98b8>{Inline(t)}</color>";
            return Inline(t);
        }

        /// <summary>`강조`와 **굵게**</summary>
        private static string Inline(string text)
        {
            text = Regex.Replace(text, "`([^`]+)`", m => $"<color=#{ColorUtility.ToHtmlStringRGB(CodeOrange)}>{m.Groups[1].Value}</color>");
            return Regex.Replace(text, @"\*\*([^*]+)\*\*", "<b>$1</b>");
        }

        private static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : t.ToUniversalTime();
    }
}
