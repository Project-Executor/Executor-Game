using System;
using System.Collections.Generic;
using System.Linq;
using Executor.Network;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 방명록 (시안 Guestbook_design: MainGuestbook, PopGuestbookChat). 전시에서 로그인 없이 남긴다.
    /// 메인 화면 왼쪽 아래 위젯(guestbook.log): 최신 4개 + "LIVE · 전체 수" + [방명록 바로가기 ▶].
    /// guestbook.exe 창(채팅 탭만): 위 탭 줄(오늘·전체 수), 채팅 기록(다른 사람 왼쪽 말풍선, 이 PC에서 남긴 글은 오른쪽 핑크 말풍선, 날짜 구분선),
    /// 오른쪽 델타와 보상 안내, 아래 이름(10자)·남길 말(100자) 입력과 [남기기 ↵]. 데이터는 WireframeGame이 몇 초마다 다시 받아 넣는다.
    /// </summary>
    public partial class MenuUI
    {
        public static readonly Color GuestPink = Hex(0xff4fa3), GuestPinkLight = Hex(0xffb3d6);
        /// <summary>아바타 색 6가지 (이름으로 정해진다)</summary>
        private static readonly Color[] AvatarColors = { Cyan, Hex(0xff4fa3), Hex(0xff9a3c), Hex(0xc3a6ff), Hex(0x5cf08a), Hex(0xffd84a) };

        public const int GuestNameMax = 10, GuestMessageMax = 100;

        /// <summary>위젯의 [방명록 바로가기]</summary>
        public Action OnGuestbook;
        /// <summary>[남기기] (이름, 남길 말). 결과는 GuestbookPosted / SetGuestbookStatus로 알려 준다</summary>
        public Action<string, string> OnGuestbookPost;

        private GuestbookLatest _gbData;
        private string _gbError;
        private string _gbSignature;

        private RectTransform _gbWidget, _gbWidgetList;
        private Text _gbWidgetLive;

        private GameObject _gbWindow;
        private RectTransform _gbLog;
        private ScrollRect _gbScroll;
        private Text _gbStats, _gbCounter;
        private InputField _gbName, _gbMessage;
        private string _gbStatusText;
        private Color _gbStatusColor;
        private float _gbStatusUntil;

        public bool IsGuestbookOpen => _gbWindow != null;

        // ---------------- 이 PC에서 남긴 글 ----------------
        private const string MineKey = "guestbook_mine";

        private static HashSet<long> MineIds() =>
            new HashSet<long>(PlayerPrefs.GetString(MineKey, "").Split(',').Where(s => long.TryParse(s, out _)).Select(long.Parse));

        public static void RememberMine(long id)
        {
            HashSet<long> ids = MineIds();
            if (!ids.Add(id)) return;
            PlayerPrefs.SetString(MineKey, string.Join(",", ids.OrderByDescending(i => i).Take(200)));
            PlayerPrefs.Save();
        }

        // ---------------- 데이터 ----------------
        /// <summary>새로 받은 방명록. 바뀐 게 있을 때만 다시 그린다</summary>
        public void SetGuestbook(GuestbookLatest data)
        {
            _gbError = null;
            // 관리자가 꺼 두었으면 위젯을 숨기고, 열려 있던 창도 닫는다
            bool on = data != null && data.Enabled;
            if (_gbWidget != null) _gbWidget.gameObject.SetActive(on);
            if (!on && IsGuestbookOpen) ClosePopups();
            string signature = data == null ? "" : $"{data.Enabled}:{data.Total}:{data.Today}:{string.Join(",", data.Entries.Select(e => e.Id))}";
            bool changed = signature != _gbSignature;
            _gbData = data;
            _gbSignature = signature;
            if (!changed) return;
            DrawGuestbookWidget();
            DrawGuestbookWindowData();
        }

        public void SetGuestbookError(string message)
        {
            if (_gbData != null) return; // 이미 보이는 글이 있으면 그대로 둔다 (잠깐 끊긴 것)
            _gbError = message;
            DrawGuestbookWidget();
        }

        /// <summary>입력 줄 오른쪽 위 상태 문구 (몇 초 뒤 글자 수로 돌아간다)</summary>
        public void SetGuestbookStatus(string text, Color color, float seconds = 3f)
        {
            _gbStatusText = text;
            _gbStatusColor = color;
            _gbStatusUntil = Time.unscaledTime + seconds;
            UpdateGuestbookCounter();
        }

        /// <summary>남기기 성공: 남길 말을 비우고 다시 입력할 수 있게</summary>
        public void GuestbookPosted()
        {
            if (_gbMessage == null) return;
            _gbMessage.text = "";
            _gbMessage.Select();
            _gbMessage.ActivateInputField();
        }

        /// <summary>WireframeGame.Update에서 매 프레임: 상태 문구가 끝나면 글자 수로</summary>
        public void TickGuestbook()
        {
            if (_gbStatusText != null && Time.unscaledTime >= _gbStatusUntil)
            {
                _gbStatusText = null;
                UpdateGuestbookCounter();
            }
        }

        // ---------------- 메인 화면 위젯 ----------------
        private void BuildGuestbookWidget(Transform screen)
        {
            const float width = 352 * S, border = 2 * S, header = 28 * S, listH = 236 * S, pad = 12 * S, btnH = 44 * S;
            float height = border + header + listH + btnH + 8 * S + 16 * S + pad + border;
            RectTransform box = Frame(screen, "Guestbook", Hex(0x0c0a18, 0.94f), GuestPink, border, 4 * S);
            At(box, 156 * S, 300 * S, width, height);
            _gbWidget = box;
            box.gameObject.SetActive(_gbData != null && _gbData.Enabled); // 서버가 켜져 있다고 알려 주기 전까지는 숨김

            Image bar = Fill(box, "Header", GuestPink);
            At(bar, border, border, width - border * 2, header);
            Text title = Label(bar.transform, "방명록 · guestbook.log", 12 * S, Ink, TextAnchor.MiddleLeft, bold: true);
            Inset(title, 10 * S, 0, 10 * S, 0);
            _gbWidgetLive = Label(bar.transform, "LIVE", 12 * S, Ink, TextAnchor.MiddleRight, UiKit.Mono);
            Inset(_gbWidgetLive, 10 * S, 0, 10 * S, 0);
            Image dot = Fill(bar.transform, "LiveDot", Ink);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(1, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(6 * S, 6 * S);
            dot.rectTransform.anchoredPosition = new Vector2(-96 * S, 0);
            UiAnimator.Blink(dot, 1.2f);

            _gbWidgetList = Node(box, "List");
            At(_gbWidgetList, border + pad, border + header + pad, width - (border + pad) * 2, listH - pad * 2);
            _gbWidgetList.gameObject.AddComponent<RectMask2D>();

            PinkButton(box, "방명록 바로가기 ▶", 12 * S, () => OnGuestbook?.Invoke(), out RectTransform go);
            At(go, border + pad, border + header + listH, width - (border + pad) * 2, btnH);
            Text hint = Label(box, "전시회에 다녀간 흔적을 남겨 주세요", 12 * S, Muted, TextAnchor.MiddleCenter);
            At(hint, 0, border + header + listH + btnH + 8 * S, width, 16 * S);
            BorderOnTop(box);
            DrawGuestbookWidget();
        }

        private void DrawGuestbookWidget()
        {
            if (_gbWidgetList == null) return;
            UiKit.Clear(_gbWidgetList);
            float w = _gbWidgetList.rect.width;
            if (_gbWidgetLive != null) _gbWidgetLive.text = _gbData == null ? "LIVE" : $"LIVE · {_gbData.Total}";
            List<GuestbookEntry> entries = _gbData?.Entries ?? new List<GuestbookEntry>();
            if (entries.Count == 0)
            {
                string msg = _gbError ?? (_gbData == null ? "불러오는 중..." : "아직 방명록이 없어요.\n첫 번째 흔적을 남겨 주세요!");
                Text empty = Label(_gbWidgetList, msg, 12 * S, Muted, TextAnchor.MiddleCenter);
                empty.lineSpacing = 1.3f;
                Inset(empty, 0, 0, 0, 0);
                return;
            }
            const float avatar = 20 * S, itemH = 16 * S + 3 * S + 28 * S, gap = 10 * S;
            var shown = entries.Skip(Math.Max(0, entries.Count - 4)).ToList();
            float y = _gbWidgetList.rect.height - shown.Count * itemH - (shown.Count - 1) * gap; // 아래에 붙여 쌓는다
            foreach (GuestbookEntry e in shown)
            {
                Color color = AvatarColor(e.Name);
                Avatar(_gbWidgetList, e.Name, color, 0, y, avatar);
                Text name = Label(_gbWidgetList, e.Name, 12 * S, color, TextAnchor.MiddleLeft, bold: true);
                At(name, avatar + 8 * S, y, w - avatar - 8 * S - 48 * S, 16 * S);
                Text time = Label(_gbWidgetList, Clock(e.CreatedAt), 12 * S, Dim, TextAnchor.MiddleRight, UiKit.Mono);
                At(time, w - 48 * S, y, 48 * S, 16 * S);
                Image bubble = Fill(_gbWidgetList, "Bubble", Track);
                At(bubble, avatar + 8 * S, y + 19 * S, w - avatar - 8 * S, 28 * S);
                Border(bubble.transform, TrackLine, 1 * S);
                Text msg = Label(bubble.transform, "", 12 * S, Fg, TextAnchor.MiddleLeft);
                Inset(msg, 8 * S, 0, 8 * S, 0);
                Ellipsize(msg, e.Message, w - avatar - 8 * S - 16 * S);
                y += itemH + gap;
            }
        }

        // ---------------- guestbook.exe 창 ----------------
        /// <param name="defaultName">이름 칸에 미리 넣을 이름 (로그인이면 닉네임, 아니면 지난번에 쓴 이름)</param>
        /// <param name="online">로그인했는지 (보상 안내 문구)</param>
        public void ShowGuestbook(string defaultName, bool online)
        {
            ClosePopups();
            const float width = 888, height = 600;
            RectTransform win = DesktopWindow("방명록 — guestbook.exe", "guestbook", "guestbook.exe", 196, 48, width, height, false, out RectTransform body);
            Image titleLine = win.Find("TitleLine")?.GetComponent<Image>();
            if (titleLine != null) titleLine.color = GuestPink;
            _gbWindow = win.gameObject;
            float W = width * S - WinBorder * 2, H = height * S - WinBorder * 2 - WinTitleH;

            // 탭 줄: 채팅 (게시판은 아직) · 오른쪽 오늘·전체 수
            Image tabs = Fill(body, "Tabs", Night);
            At(tabs, 0, 0, W, 40 * S);
            Image tabsLine = Fill(body, "TabsLine", TrackLine);
            At(tabsLine, 0, 40 * S, W, 2 * S);
            Text chat = Label(tabs.transform, "채팅", 12 * S, Fg, TextAnchor.MiddleCenter, bold: true);
            At(chat, 8 * S, 0, 64 * S, 40 * S);
            Image under = Fill(tabs.transform, "Under", GuestPink);
            At(under, 8 * S, 38 * S, 64 * S, 2 * S);
            _gbStats = Label(tabs.transform, "", 12 * S, Muted, TextAnchor.MiddleRight);
            Inset(_gbStats, 80 * S, 0, 16 * S, 0);

            const float top = 42 * S, formH = 12 * S + 16 * S + 4 * S + 36 * S + 12 * S, asideW = 168 * S;
            float formTop = H - formH - 2 * S, mainH = formTop - top;

            // 오른쪽: 델타 + 보상 안내
            Image aside = Fill(body, "Aside", Night);
            At(aside, W - asideW, top, asideW, mainH);
            Image asideLine = Fill(body, "AsideLine", TrackLine);
            At(asideLine, W - asideW, top, 2 * S, mainH);
            Sprite deltaArt = ArtCatalog.Ui("delta/base");
            if (deltaArt != null)
            {
                Image delta = Fill(aside.transform, "Delta", Color.white);
                delta.sprite = deltaArt;
                delta.preserveAspect = true;
                At(delta, (asideW - 99 * S) / 2, 16 * S, 99 * S, 132 * S);
            }
            float boxW = asideW - 24 * S;
            Image reward = Fill(aside.transform, "Reward", Ink);
            At(reward, 12 * S, 16 * S + 132 * S + 10 * S, boxW, 64 * S);
            Border(reward.transform, Orange, 2 * S);
            Text rewardText = Label(reward.transform, online
                    ? "방명록을 남기면\n<color=#ffd84a>코인 ×10</color>을 드려요!\n<color=#8b98b8>(하루 한 번)</color>"
                    : "로그인하고 남기면\n<color=#ffd84a>코인 ×10</color>을 드려요!\n<color=#8b98b8>(하루 한 번)</color>",
                12 * S, Fg, TextAnchor.MiddleCenter);
            rewardText.lineSpacing = 1.2f;
            Inset(rewardText, 4 * S, 4 * S, 4 * S, 4 * S);
            Text note = Label(aside.transform, "욕설·개인정보는\n자동으로 가려져요", 12 * S, Dim, TextAnchor.LowerCenter);
            note.lineSpacing = 1.3f;
            At(note, 0, mainH - 16 * S - 40 * S, asideW, 40 * S);

            // 가운데: 채팅 기록 (아래부터 쌓이고 새 글이 오면 맨 아래로)
            RectTransform logArea = Node(body, "Log");
            At(logArea, 0, top, W - asideW, mainH);
            _gbLog = ScrollList(logArea, out _gbScroll);
            Inset(_gbLog.parent, 20 * S, 16 * S, 20 * S, 16 * S);

            // 아래: 이름 · 남길 말 · [남기기]
            Image form = Fill(body, "Form", Night);
            At(form, 0, formTop, W, formH + 2 * S);
            Image formLine = Fill(body, "FormLine", TrackLine);
            At(formLine, 0, formTop, W, 2 * S);
            float fy = formTop + 2 * S + 12 * S, nameW = 148 * S, btnW = 112 * S;
            float msgX = 16 * S + nameW + 8 * S, msgW = W - msgX - 8 * S - btnW - 16 * S;
            Text nameLabel = Label(body, "이름", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(nameLabel, 16 * S, fy, nameW, 16 * S);
            Text msgLabel = Label(body, "남길 말", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(msgLabel, msgX, fy, msgW, 16 * S);
            _gbCounter = Label(body, "", 12 * S, Dim, TextAnchor.MiddleRight, UiKit.Mono);
            At(_gbCounter, msgX, fy, msgW, 16 * S);
            fy += 16 * S + 4 * S;
            _gbName = GuestInput(body, "이름", GuestNameMax);
            At(_gbName, 16 * S, fy, nameW, 36 * S);
            _gbName.text = defaultName ?? "";
            _gbMessage = GuestInput(body, "전시 소감, 응원 한마디를 남겨 주세요", GuestMessageMax);
            At(_gbMessage, msgX, fy, msgW, 36 * S);
            _gbMessage.onValueChanged.AddListener(_ => UpdateGuestbookCounter());
            UiKit.OnEnter(_gbMessage, SubmitGuestbook);
            UiKit.OnEnter(_gbName, () => { _gbMessage.Select(); _gbMessage.ActivateInputField(); });
            PinkButton(body, "남기기 ↵", 12 * S, SubmitGuestbook, out RectTransform send);
            At(send, W - 16 * S - btnW, fy, btnW, 36 * S);

            _gbStatusText = null;
            UpdateGuestbookCounter();
            DrawGuestbookWindowData(forceBottom: true);
            InputField first = string.IsNullOrEmpty(_gbName.text) ? _gbName : _gbMessage;
            first.Select();
            first.ActivateInputField();
        }

        private void SubmitGuestbook()
        {
            if (_gbName == null || _gbMessage == null) return;
            string name = _gbName.text.Trim(), message = _gbMessage.text.Trim();
            if (name.Length == 0) { SetGuestbookStatus("이름을 입력해 주세요", Red); _gbName.Select(); _gbName.ActivateInputField(); return; }
            if (message.Length == 0) { SetGuestbookStatus("남길 말을 입력해 주세요", Red); return; }
            OnGuestbookPost?.Invoke(name, message);
        }

        private void UpdateGuestbookCounter()
        {
            if (_gbCounter == null) return;
            if (_gbStatusText != null)
            {
                _gbCounter.text = _gbStatusText;
                _gbCounter.color = _gbStatusColor;
                _gbCounter.font = UiKit.Regular;
                return;
            }
            _gbCounter.font = UiKit.Mono;
            _gbCounter.color = Dim;
            _gbCounter.text = $"{_gbMessage?.text.Length ?? 0}/{GuestMessageMax}";
        }

        private void DrawGuestbookWindowData(bool forceBottom = false)
        {
            if (_gbLog == null || _gbWindow == null) return;
            if (_gbStats != null)
            {
                _gbStats.text = "2026 동아리 전시회 · <color=#eaf2ff>EXECUTOR 부스</color>      " + (_gbData == null ? ""
                    : $"오늘 <color=#ff4fa3>{_gbData.Today}</color> · 전체 <color=#eaf2ff>{_gbData.Total}</color>");
            }
            bool atBottom = forceBottom || _gbScroll == null || _gbScroll.verticalNormalizedPosition <= 0.02f;
            UiKit.Clear(_gbLog);
            var viewport = (RectTransform)_gbLog.parent;
            float W = viewport.rect.width, viewH = viewport.rect.height;
            List<GuestbookEntry> entries = _gbData?.Entries ?? new List<GuestbookEntry>();
            if (entries.Count == 0)
            {
                Text empty = Label(_gbLog, _gbData == null ? (_gbError ?? "불러오는 중...") : "아직 방명록이 없어요. 아래에서 첫 번째 흔적을 남겨 주세요!",
                    12 * S, Muted, TextAnchor.MiddleCenter);
                At(empty, 0, 0, W, viewH);
                _gbLog.sizeDelta = new Vector2(0, viewH);
                return;
            }

            HashSet<long> mine = MineIds();
            const float gap = 14 * S, avatar = 28 * S, padX = 12 * S, padY = 8 * S;
            var items = new List<Action<float>>();
            var heights = new List<float>();
            DateTime? lastDay = null;
            foreach (GuestbookEntry e in entries)
            {
                DateTime local = Local(e.CreatedAt);
                if (lastDay != local.Date)
                {
                    lastDay = local.Date;
                    string day = $"{local:M월 d일} ({"일월화수목금토"[(int)local.DayOfWeek]})";
                    heights.Add(16 * S);
                    items.Add(y => DaySeparator(_gbLog, day, y, W));
                }
                bool me = mine.Contains(e.Id);
                float maxText = (me ? 420 : 460) * S - padX * 2;
                Text probe = Label(_gbLog, e.Message, 12 * S, Fg, TextAnchor.UpperLeft);
                probe.horizontalOverflow = HorizontalWrapMode.Overflow;
                float textW = Mathf.Min(probe.preferredWidth + 1, maxText);
                probe.horizontalOverflow = HorizontalWrapMode.Wrap;
                probe.lineSpacing = 1.25f;
                probe.rectTransform.sizeDelta = new Vector2(textW, 0);
                float textH = Mathf.Max(20 * S, probe.preferredHeight);
                UnityEngine.Object.Destroy(probe.gameObject);
                float bubbleW = textW + padX * 2, bubbleH = textH + padY * 2;
                heights.Add(16 * S + 4 * S + bubbleH);
                GuestbookEntry entry = e;
                items.Add(y => ChatEntry(_gbLog, entry, me, y, W, avatar, bubbleW, bubbleH, textW, textH, padX, padY));
            }
            float total = heights.Sum() + gap * (heights.Count - 1);
            float content = Mathf.Max(total, viewH);
            float cy = content - total; // 기록이 적으면 아래에 붙인다
            for (int i = 0; i < items.Count; i++)
            {
                items[i](cy);
                cy += heights[i] + gap;
            }
            _gbLog.sizeDelta = new Vector2(0, content);
            if (atBottom && _gbScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _gbScroll.verticalNormalizedPosition = 0;
            }
        }

        private static void DaySeparator(RectTransform parent, string text, float y, float w)
        {
            Text t = Label(parent, text, 12 * S, Dim, TextAnchor.MiddleCenter);
            float tw = t.preferredWidth + 24 * S;
            At(t, (w - tw) / 2, y, tw, 16 * S);
            Image left = Fill(parent, "Line", TrackLine);
            At(left, 0, y + 7 * S, (w - tw) / 2, 2 * S);
            Image right = Fill(parent, "Line", TrackLine);
            At(right, (w + tw) / 2, y + 7 * S, (w - tw) / 2, 2 * S);
        }

        private static void ChatEntry(RectTransform parent, GuestbookEntry e, bool me, float y, float w, float avatar,
            float bubbleW, float bubbleH, float textW, float textH, float padX, float padY)
        {
            DateTime local = Local(e.CreatedAt);
            if (me)
            {
                // 내 글: 오른쪽 핑크 말풍선, 이름 줄 오른쪽 정렬, 시각은 말풍선 왼쪽 아래
                bool justNow = (DateTime.Now - local).TotalSeconds < 60;
                Text head = Label(parent, $"<b>{e.Name}</b>" + (justNow ? " <color=#4b5675>· 방금 남김</color>" : ""), 12 * S, GuestPink, TextAnchor.MiddleRight);
                At(head, 0, y, w, 16 * S);
                Image shadow = Fill(parent, "Shadow", Ink);
                At(shadow, w - bubbleW + 3 * S, y + 20 * S + 3 * S, bubbleW, bubbleH);
                Image bubble = Fill(parent, "Bubble", GuestPink);
                At(bubble, w - bubbleW, y + 20 * S, bubbleW, bubbleH);
                Text msg = Label(bubble.transform, e.Message, 12 * S, Ink, TextAnchor.UpperLeft);
                msg.horizontalOverflow = HorizontalWrapMode.Wrap;
                msg.lineSpacing = 1.25f;
                At(msg, padX, padY, textW, textH);
                Text time = Label(parent, local.ToString("HH:mm"), 12 * S, Dim, TextAnchor.LowerRight, UiKit.Mono);
                At(time, w - bubbleW - 8 * S - 48 * S, y + 20 * S + bubbleH - 16 * S, 48 * S, 16 * S);
                return;
            }
            Color color = AvatarColor(e.Name);
            Avatar(parent, e.Name, color, 0, y, avatar);
            float x = avatar + 10 * S;
            Text name = Label(parent, $"<b>{e.Name}</b>  <color=#4b5675>{local:HH:mm}</color>", 12 * S, color, TextAnchor.MiddleLeft);
            At(name, x, y, w - x, 16 * S);
            Image sh = Fill(parent, "Shadow", Ink);
            At(sh, x + 3 * S, y + 20 * S + 3 * S, bubbleW, bubbleH);
            Image b = Fill(parent, "Bubble", Track);
            At(b, x, y + 20 * S, bubbleW, bubbleH);
            Border(b.transform, TrackLine, 1 * S);
            Text m = Label(b.transform, e.Message, 12 * S, Fg, TextAnchor.UpperLeft);
            m.horizontalOverflow = HorizontalWrapMode.Wrap;
            m.lineSpacing = 1.25f;
            At(m, padX, padY, textW, textH);
        }

        // ---------------- 부품 ----------------
        /// <summary>아바타: 이름의 한 글자(끝에서 두 번째 글자, 한 글자 이름은 그 글자) + 이름으로 정한 색</summary>
        private static void Avatar(Transform parent, string name, Color color, float x, float y, float size)
        {
            Image face = Fill(parent, "Avatar", color);
            At(face, x, y, size, size);
            Image shadeR = Fill(face.transform, "Shade", Hex(0x0c0a18, 0.28f));
            At(shadeR, size - 3 * S, 0, 3 * S, size);
            Image shadeB = Fill(face.transform, "Shade", Hex(0x0c0a18, 0.28f));
            At(shadeB, 0, size - 3 * S, size - 3 * S, 3 * S);
            Text letter = Label(face.transform, AvatarLetter(name), 12 * S, Ink, TextAnchor.MiddleCenter, bold: true);
            Inset(letter, 0, 0, 0, 0);
        }

        public static string AvatarLetter(string name)
        {
            string s = (name ?? "").Replace(" ", "");
            if (s.Length == 0) return "?";
            return s.Length == 1 ? s : s.Substring(s.Length - 2, 1);
        }

        public static Color AvatarColor(string name)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in name ?? "") h = h * 31 + c;
                return AvatarColors[(h & 0x7fffffff) % AvatarColors.Length];
            }
        }

        private static DateTime Local(DateTime t) =>
            (t.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : t).ToLocalTime();

        private static string Clock(DateTime t) => Local(t).ToString("HH:mm");

        /// <summary>한 줄에 들어가게 줄이고 끝에 … (말풍선 미리 보기)</summary>
        private static void Ellipsize(Text t, string text, float width)
        {
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = text ?? "";
            if (t.preferredWidth <= width) return;
            int n = t.text.Length;
            while (n > 0)
            {
                n--;
                t.text = text.Substring(0, n).TrimEnd() + "…";
                if (t.preferredWidth <= width) return;
            }
        }

        /// <summary>핑크 버튼 (방명록 강조색, 그림자 있음)</summary>
        private static Button PinkButton(Transform parent, string text, float fontSize, Action onClick, out RectTransform root)
        {
            root = Node(parent, "Pink:" + text);
            Image shadow = Fill(root, "Shadow", Ink);
            Inset(shadow, 3 * S, 3 * S, -3 * S, -3 * S);
            Image face = Fill(root, "Face", Color.white);
            Inset(face, 0, 0, 0, 0);
            Button button = Clickable(face, onClick, GuestPink, GuestPinkLight);
            Text label = Label(face.transform, text, fontSize, Ink, TextAnchor.MiddleCenter, bold: true);
            Inset(label, 0, 0, 0, 0);
            return button;
        }

        /// <summary>입력칸 (어두운 바탕, 초점이면 핑크 테두리는 FieldFrame이 그린다)</summary>
        private static InputField GuestInput(Transform parent, string placeholder, int limit)
        {
            InputField field = UiKit.Input(parent, placeholder, limit: limit, size: Mathf.RoundToInt(12 * S));
            FieldFrame.Apply(field);
            if (field.GetComponent<Image>() is Image bg) bg.color = Ink;
            foreach (Text t in new[] { field.textComponent, field.placeholder as Text })
            {
                if (t == null) continue;
                t.font = UiKit.Regular;
                t.rectTransform.offsetMin = new Vector2(10 * S, 0);
                t.rectTransform.offsetMax = new Vector2(-10 * S, 0);
            }
            return field;
        }
    }
}
