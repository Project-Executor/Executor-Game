using System;
using System.Collections.Generic;
using System.Linq;
using Executor.Data;
using Executor.Network;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.UiKit;

namespace Executor.Play
{
    /// <summary>
    /// 신고·버그 제보 팝업과 내 신고 목록. 대화 화면(사용자 신고), 일시 정지 메뉴(이 스테이지 버그 제보),
    /// 메인 설정(버그 제보·내 신고)에서 연다. 팝업 자리는 부르는 쪽이 정한다.
    /// </summary>
    public static class ReportPanel
    {
        private const int TitleMax = 255, CommentMax = 2000;

        private static List<ReportCategory> _categories;

        /// <summary>대상: 사용자 또는 스테이지 (둘 다 null이면 대상 없음 = 버그 제보·기타만)</summary>
        public class Target
        {
            public long? UserId;
            public long? StageId;
            /// <summary>화면에 보일 대상 이름 (예: "사용자 nullptr", "스테이지 Tutorial 3. 열쇠")</summary>
            public string Label;
            /// <summary>처음에 고를 카테고리 이름 (없으면 첫 번째)</summary>
            public string PreferCategory;
            /// <summary>제목 칸에 미리 넣을 내용</summary>
            public string Title;
        }

        public static async void Open(Transform root, Target target)
        {
            if (!PlayerSession.IsOnline)
            {
                Message(root, "신고", "신고와 버그 제보는 로그인하면 쓸 수 있습니다.");
                return;
            }
            try
            {
                _categories ??= await ReportApi.GetCategoriesAsync();
            }
            catch (ApiException e)
            {
                Message(root, "신고", e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message);
                return;
            }
            if (root == null) return;
            Build(root, target ?? new Target());
        }

        private static void Build(Transform root, Target target)
        {
            bool hasTarget = target.UserId != null || target.StageId != null;
            // 대상이 없으면 대상이 필요 없는 카테고리(버그 제보·기타)만
            List<ReportCategory> categories = _categories.Where(c => hasTarget || !c.TargetRequired).ToList();
            ReportCategory selected = categories.FirstOrDefault(c => c.Name == target.PreferCategory) ?? categories.FirstOrDefault();

            GameObject popup = Popup(root, hasTarget ? "신고" : "버그 제보", new Vector2(1100, 860), out RectTransform body);
            var who = Label(body, hasTarget ? $"대상: {target.Label}" : "대상 없이 보냅니다 (게임 전체에 대한 제보)", 24, TextAnchor.MiddleLeft, Muted);
            Place(who, new Vector2(0, 1), Vector2.zero, new Vector2(1020, 40), new Vector2(0, 1));

            // 카테고리 (버튼 한 줄, 고른 것은 강조)
            var tabs = new List<(ReportCategory c, Button b)>();
            float x = 0;
            foreach (ReportCategory c in categories)
            {
                ReportCategory cat = c;
                var b = Button(body, c.Name, null, 22);
                Place(b, new Vector2(0, 1), new Vector2(x, -56), new Vector2(240, 56), new Vector2(0, 1));
                tabs.Add((c, b));
                x += 252;
            }
            void Highlight()
            {
                foreach (var (c, b) in tabs) b.GetComponent<Image>().color = c == selected ? Line : ButtonColor;
            }
            foreach (var (c, b) in tabs)
            {
                ReportCategory cat = c;
                b.onClick.AddListener(() => { selected = cat; Highlight(); });
            }
            Highlight();

            var titleLabel = Label(body, $"제목 (1~{TitleMax}자)", 22, TextAnchor.MiddleLeft);
            Place(titleLabel, new Vector2(0, 1), new Vector2(0, -128), new Vector2(600, 34), new Vector2(0, 1));
            InputField title = Input(body, "한 줄로 요약", limit: TitleMax, size: 26);
            Place(title, new Vector2(0, 1), new Vector2(0, -166), new Vector2(1020, 64), new Vector2(0, 1));
            title.text = target.Title ?? "";

            var commentLabel = Label(body, $"내용 (1~{CommentMax}자)", 22, TextAnchor.MiddleLeft);
            Place(commentLabel, new Vector2(0, 1), new Vector2(0, -246), new Vector2(600, 34), new Vector2(0, 1));
            InputField comment = Input(body, "무슨 일이 있었는지, 어디서 어떻게 하면 생기는지 적어 주세요.", limit: CommentMax, size: 24);
            comment.lineType = InputField.LineType.MultiLineNewline;
            comment.textComponent.alignment = TextAnchor.UpperLeft;
            ((Text)comment.placeholder).alignment = TextAnchor.UpperLeft;
            Place(comment, new Vector2(0, 1), new Vector2(0, -284), new Vector2(1020, 300), new Vector2(0, 1));

            var status = Label(body, "", 22, TextAnchor.MiddleLeft, Bad);
            Place(status, new Vector2(0, 0), new Vector2(0, 10), new Vector2(760, 50), new Vector2(0, 0));
            Button send = null;
            send = Button(body, "보내기", async () =>
            {
                string t = title.text.Trim(), m = comment.text.Trim();
                if (selected == null) { status.text = "카테고리를 골라 주세요."; return; }
                if (t.Length < 1) { status.text = "제목을 입력해 주세요."; return; }
                if (m.Length < 1) { status.text = "내용을 입력해 주세요."; return; }
                send.interactable = false;
                status.color = Muted;
                status.text = "보내는 중...";
                try
                {
                    ReportResponse res = await ReportApi.CreateAsync(new ReportRequest
                    {
                        CategoryId = selected.CategoryId,
                        TargetUserId = target.UserId,
                        TargetStageId = target.UserId == null ? target.StageId : null,
                        Title = t,
                        Comment = m,
                    });
                    if (popup != null) UnityEngine.Object.Destroy(popup);
                    Message(root, "신고", $"접수되었습니다. (#{res.ReportId})\n처리 상황은 메인 화면 설정의 '내 신고'에서 볼 수 있습니다.");
                }
                catch (ApiException e)
                {
                    if (send == null) return;
                    send.interactable = true;
                    status.color = Bad;
                    status.text = e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message;
                }
            }, 26);
            Place(send, new Vector2(1, 0), Vector2.zero, new Vector2(220, 72), new Vector2(1, 0));
            title.Select();
            title.ActivateInputField();
        }

        /// <summary>내 신고 목록 (최근 20개, 처리 상태)</summary>
        public static async void OpenMine(Transform root)
        {
            if (!PlayerSession.IsOnline)
            {
                Message(root, "내 신고", "로그인하면 볼 수 있습니다.");
                return;
            }
            try
            {
                PageResponse<MyReport> page = await ReportApi.GetMineAsync();
                if (root == null) return;
                Popup(root, "내 신고", new Vector2(1100, 820), out RectTransform body);
                if (page.Content.Count == 0)
                {
                    var empty = Label(body, "보낸 신고가 없습니다.", 28, TextAnchor.MiddleCenter, Muted);
                    Stretch(empty.rectTransform);
                    return;
                }
                float y = 0;
                foreach (MyReport r in page.Content.Take(8))
                {
                    var row = Panel(body, "Report", new Color32(0x16, 0x1e, 0x2a, 0xff));
                    Place(row, new Vector2(0, 1), new Vector2(0, y), new Vector2(1020, 72), new Vector2(0, 1));
                    var t = Label(row, $"[{r.Category}] {r.Title}", 24, TextAnchor.UpperLeft, bold: true);
                    Stretch(t.rectTransform, 10);
                    t.rectTransform.offsetMax = new Vector2(-200, -8);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var d = Label(row, $"{r.CreatedAt.ToLocalTime():yyyy.MM.dd HH:mm}" + (r.HandledAt != null ? $" · 처리 {r.HandledAt.Value.ToLocalTime():MM.dd HH:mm}" : ""),
                        18, TextAnchor.LowerLeft, Muted);
                    Stretch(d.rectTransform, 10);
                    var s = Label(row, r.StatusLabel, 24, TextAnchor.MiddleRight,
                        r.Status == "DONE" ? Good : r.Status == "IN_PROGRESS" ? Warn : Muted, bold: true);
                    Stretch(s.rectTransform, 16);
                    y -= 82;
                }
                if (page.TotalElements > 8)
                {
                    var more = Label(body, $"외 {page.TotalElements - 8}개 (최근 8개만 표시)", 20, TextAnchor.UpperLeft, Muted);
                    Place(more, new Vector2(0, 1), new Vector2(0, y - 4), new Vector2(1020, 30), new Vector2(0, 1));
                }
            }
            catch (ApiException e)
            {
                Message(root, "내 신고", e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message);
            }
        }

        private static void Message(Transform root, string title, string text)
        {
            if (root == null) return;
            Popup(root, title, new Vector2(760, 360), out RectTransform body);
            var t = Label(body, text, 26, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
        }
    }
}
