using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Executor.Data;
using Executor.Network;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.UiKit;

namespace Executor.Play
{
    /// <summary>
    /// 친구·메시지 팝업. 메인 화면(친구 버튼)과 플레이 화면(채팅 버튼)이 같은 화면을 쓴다.
    /// 탭: 메시지(대화 상대) · 친구 · 받은 요청 · 보낸 요청 · 친구 찾기. 대화를 열면 같은 팝업 안에서 대화 화면으로 바뀐다.
    /// 대화 화면은 열려 있는 동안 몇 초마다 새 메시지를 불러온다 (Tick).
    /// </summary>
    public class SocialPanel
    {
        public const string TabMessages = "messages", TabFriends = "friends", TabReceived = "received", TabSent = "sent", TabSearch = "search";

        private const float PopupW = 1240, PopupH = 900;
        private const float PollSeconds = 5f;
        private const int MessageMax = 500;

        private readonly Func<Transform> _root;
        private readonly Action<int> _onBadge;

        private GameObject _popup;
        private RectTransform _body;
        private Text _status;
        private string _tab = TabMessages;
        private int _received, _unread;

        // 대화 화면
        private UserSummary _partner;
        private RectTransform _chatList;
        private ScrollRect _chatScroll;
        private InputField _chatInput;
        private float _pollLeft;
        private bool _loadingChat;
        private long _lastMessageId;

        /// <param name="popupRoot">팝업을 붙일 곳 (메뉴 또는 게임 화면의 팝업 자리)</param>
        /// <param name="onBadge">안 읽은 메시지 + 받은 요청 수가 바뀌면 (버튼 숫자 표시용)</param>
        public SocialPanel(Func<Transform> popupRoot, Action<int> onBadge)
        {
            _root = popupRoot;
            _onBadge = onBadge;
        }

        public bool IsOpen => _popup != null;

        /// <summary>팝업을 연다 (이미 열려 있으면 탭만 바꾼다)</summary>
        public void Open(string tab = TabMessages)
        {
            if (!PlayerSession.IsOnline)
            {
                Build("친구·메시지");
                Note("친구와 메시지는 로그인하면 쓸 수 있습니다.");
                return;
            }
            _tab = tab;
            _partner = null;
            Build("친구·메시지");
            ShowTab();
        }

        public void Close()
        {
            if (_popup != null) UnityEngine.Object.Destroy(_popup);
            _popup = null;
            _partner = null;
        }

        /// <summary>대화 화면이 열려 있으면 PollSeconds마다 새 메시지를 불러온다</summary>
        public void Tick(float deltaTime)
        {
            if (_popup == null || _partner == null) return;
            _pollLeft -= deltaTime;
            if (_pollLeft > 0) return;
            _pollLeft = PollSeconds;
            LoadChat(false);
        }

        /// <summary>버튼 숫자(안 읽은 메시지 + 받은 요청)를 서버에서 다시 센다</summary>
        public async void RefreshBadge()
        {
            if (!PlayerSession.IsOnline) { _onBadge?.Invoke(0); return; }
            try
            {
                var unread = MessageApi.GetUnreadCountAsync();
                var received = FriendApi.GetReceivedAsync();
                _unread = (int)(await unread).Count;
                _received = (await received).Count;
                _onBadge?.Invoke(_unread + _received);
            }
            catch (ApiException e)
            {
                Debug.LogWarning("[SocialPanel] 알림 수를 불러오지 못했습니다: " + e.Message);
            }
        }

        // ================= 틀 =================

        private void Build(string title)
        {
            if (_popup != null) UnityEngine.Object.Destroy(_popup);
            _popup = Popup(_root(), title, new Vector2(PopupW, PopupH), out _body, () => { _popup = null; _partner = null; RefreshBadge(); });
        }

        private void ClearBody()
        {
            Clear(_body);
            _chatList = null;
            _chatInput = null;
        }

        private void Note(string text)
        {
            var t = Label(_body, text, 28, TextAnchor.MiddleCenter, Muted);
            Stretch(t.rectTransform);
        }

        /// <summary>아래쪽 안내 줄 (성공은 초록, 실패는 빨강)</summary>
        private void Status(string text, bool good)
        {
            if (_status == null) return;
            _status.text = text ?? "";
            _status.color = good ? Good : Bad;
        }

        private static string Error(ApiException e) => e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message;

        private static string Name(UserSummary u) => string.IsNullOrEmpty(u?.Nickname) ? "(닉네임 없음)" : u.Nickname;

        /// <summary>세로로 스크롤되는 목록 자리. 반환값(content)에 위에서부터 y를 음수로 내려가며 넣고, 다 넣으면 content.sizeDelta.y를 높이로</summary>
        private static RectTransform ScrollArea(RectTransform parent, float top, float bottom, out ScrollRect scroll)
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            viewport.SetParent(parent, false);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(0, bottom);
            viewport.offsetMax = new Vector2(0, -top);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            return content;
        }

        // ================= 탭 =================

        private void ShowTab()
        {
            ClearBody();
            float x = 0;
            foreach (var (tab, label) in new[]
                     {
                         (TabMessages, _unread > 0 ? $"메시지 ({_unread})" : "메시지"), (TabFriends, "친구"),
                         (TabReceived, _received > 0 ? $"받은 요청 ({_received})" : "받은 요청"), (TabSent, "보낸 요청"), (TabSearch, "친구 찾기"),
                     })
            {
                string t = tab;
                var b = Button(_body, label, () => { _tab = t; ShowTab(); }, 22, t == _tab ? (Color?)Line : null);
                Place(b, new Vector2(0, 1), new Vector2(x, 0), new Vector2(214, 56), new Vector2(0, 1));
                x += 224;
            }
            _status = Label(_body, "", 22, TextAnchor.LowerLeft, Good);
            Place(_status, new Vector2(0, 0), Vector2.zero, new Vector2(PopupW - 80, 32), new Vector2(0, 0));
            var loading = Label(_body, "불러오는 중...", 24, TextAnchor.MiddleCenter, Muted);
            Stretch(loading.rectTransform);
            switch (_tab)
            {
                case TabFriends: LoadFriends(loading); break;
                case TabReceived: LoadRequests(loading, true); break;
                case TabSent: LoadRequests(loading, false); break;
                case TabSearch: Destroy(loading); BuildSearch(); break;
                default: LoadRooms(loading); break;
            }
        }

        private static void Destroy(Component c)
        {
            if (c != null) UnityEngine.Object.Destroy(c.gameObject);
        }

        /// <summary>목록 한 줄: 왼쪽에 제목·설명, 오른쪽에 버튼들 (오른쪽부터)</summary>
        private static RectTransform Row(RectTransform list, ref float y, string title, string sub, Color? titleColor,
            params (string label, Action onClick, Color? color)[] buttons)
        {
            var row = Panel(list, "Row", new Color32(0x16, 0x1e, 0x2a, 0xff));
            Place(row, new Vector2(0, 1), new Vector2(0, y), new Vector2(PopupW - 80, 84), new Vector2(0, 1));
            float right = 12 + buttons.Length * 150;
            var t = Label(row, title, 26, TextAnchor.UpperLeft, titleColor, bold: true);
            Stretch(t.rectTransform, 12);
            t.rectTransform.offsetMax = new Vector2(-right, -12);
            if (!string.IsNullOrEmpty(sub))
            {
                var s = Label(row, sub, 20, TextAnchor.LowerLeft, Muted);
                Stretch(s.rectTransform, 12);
                s.rectTransform.offsetMax = new Vector2(-right, -12);
                s.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            float bx = -12;
            foreach (var (label, onClick, color) in buttons)
            {
                var b = Button(row, label, onClick, 22, color);
                Place(b, new Vector2(1, 0.5f), new Vector2(bx, 0), new Vector2(140, 60), new Vector2(1, 0.5f));
                bx -= 150;
            }
            y -= 94;
            return row;
        }

        private RectTransform ListArea(out ScrollRect scroll) => ScrollArea(_body, 76, 44, out scroll);

        private async void LoadRooms(Text loading)
        {
            try
            {
                List<MessageRoom> rooms = await MessageApi.GetRoomsAsync();
                if (_popup == null || _tab != TabMessages || _partner != null) return;
                Destroy(loading);
                if (rooms.Count == 0) { Note("아직 대화가 없습니다. 친구 탭에서 메시지를 보낼 수 있습니다."); return; }
                var list = ListArea(out _);
                float y = 0;
                foreach (MessageRoom r in rooms)
                {
                    UserSummary partner = r.Partner;
                    string title = Name(partner) + (r.UnreadCount > 0 ? $"   새 메시지 {r.UnreadCount}" : "");
                    Row(list, ref y, title, $"{r.LastMessage}   · {r.LastMessageAt.ToLocalTime():MM.dd HH:mm}",
                        r.UnreadCount > 0 ? Warn : (Color?)null, ("열기", () => OpenChat(partner), null));
                }
                list.sizeDelta = new Vector2(0, -y);
            }
            catch (ApiException e) { if (loading != null) loading.text = Error(e); }
        }

        private async void LoadFriends(Text loading)
        {
            try
            {
                List<FriendResponse> friends = await FriendApi.GetFriendsAsync();
                if (_popup == null || _tab != TabFriends) return;
                Destroy(loading);
                if (friends.Count == 0) { Note("아직 친구가 없습니다. '친구 찾기'에서 닉네임으로 찾아 요청을 보내 보세요."); return; }
                var list = ListArea(out _);
                float y = 0;
                foreach (FriendResponse f in friends)
                {
                    UserSummary user = f.User;
                    Row(list, ref y, Name(user), $"{f.Since.ToLocalTime():yyyy.MM.dd}부터 친구", null,
                        ("끊기", () => ConfirmUnfriend(user), new Color32(0x3a, 0x22, 0x28, 0xff)),
                        ("메시지", () => OpenChat(user), null));
                }
                list.sizeDelta = new Vector2(0, -y);
                Status($"친구 {friends.Count}명", true);
            }
            catch (ApiException e) { if (loading != null) loading.text = Error(e); }
        }

        private void ConfirmUnfriend(UserSummary user)
        {
            var popup = Popup(_root(), "친구 끊기", new Vector2(760, 360), out RectTransform body);
            var t = Label(body, $"{Name(user)} 님과 친구를 끊을까요?\n지난 대화는 남고, 다시 요청할 수 있습니다.", 26, TextAnchor.UpperCenter);
            Stretch(t.rectTransform);
            var yes = Button(body, "끊기", async () =>
            {
                UnityEngine.Object.Destroy(popup);
                try
                {
                    await FriendApi.DeleteFriendAsync(user.UserId);
                    if (_popup == null) return;
                    ShowTab();
                    Status($"{Name(user)} 님과 친구를 끊었습니다.", true);
                }
                catch (ApiException e) { Status(Error(e), false); }
            }, 24, new Color32(0x5a, 0x22, 0x2c, 0xff));
            Place(yes, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(220, 64), new Vector2(0.5f, 0));
        }

        private async void LoadRequests(Text loading, bool received)
        {
            string tab = received ? TabReceived : TabSent;
            try
            {
                List<FriendRequestItem> items = received ? await FriendApi.GetReceivedAsync() : await FriendApi.GetSentAsync();
                if (_popup == null || _tab != tab) return;
                Destroy(loading);
                if (received) { _received = items.Count; _onBadge?.Invoke(_unread + _received); }
                if (items.Count == 0) { Note(received ? "받은 친구 요청이 없습니다." : "대기 중인 보낸 요청이 없습니다."); return; }
                var list = ListArea(out _);
                float y = 0;
                foreach (FriendRequestItem r in items)
                {
                    FriendRequestItem req = r;
                    string sub = $"{r.CreatedAt.ToLocalTime():MM.dd HH:mm}에 {(received ? "받음" : "보냄")}";
                    if (received)
                        Row(list, ref y, Name(r.User), sub, null,
                            ("거절", () => Answer(req, false), new Color32(0x3a, 0x22, 0x28, 0xff)),
                            ("수락", () => Answer(req, true), null));
                    else
                        Row(list, ref y, Name(r.User), sub, null, ("취소", () => Cancel(req), null));
                }
                list.sizeDelta = new Vector2(0, -y);
            }
            catch (ApiException e) { if (loading != null) loading.text = Error(e); }
        }

        private async void Answer(FriendRequestItem req, bool accept)
        {
            try
            {
                if (accept) await FriendApi.AcceptAsync(req.RequestId);
                else await FriendApi.RejectAsync(req.RequestId);
                if (_popup == null) return;
                ShowTab();
                Status(accept ? $"{Name(req.User)} 님과 친구가 되었습니다." : $"{Name(req.User)} 님의 요청을 거절했습니다.", true);
            }
            catch (ApiException e) { Status(Error(e), false); }
        }

        private async void Cancel(FriendRequestItem req)
        {
            try
            {
                await FriendApi.CancelAsync(req.RequestId);
                if (_popup == null) return;
                ShowTab();
                Status($"{Name(req.User)} 님에게 보낸 요청을 취소했습니다.", true);
            }
            catch (ApiException e) { Status(Error(e), false); }
        }

        // ================= 친구 찾기 =================

        private RectTransform _searchList;

        private void BuildSearch()
        {
            var input = Input(_body, "닉네임 일부를 입력하고 Enter", limit: 20, size: 26);
            Place(input, new Vector2(0, 1), new Vector2(0, -76), new Vector2(PopupW - 80 - 180, 64), new Vector2(0, 1));
            var go = Button(_body, "검색", () => Search(input.text), 24);
            Place(go, new Vector2(1, 1), new Vector2(0, -76), new Vector2(160, 64), new Vector2(1, 1));
            OnEnter(input, () => Search(input.text));
            _searchList = ScrollArea(_body, 160, 44, out _);
            input.Select();
            input.ActivateInputField();
        }

        private async void Search(string text)
        {
            string q = (text ?? "").Trim();
            if (q.Length == 0) { Status("검색어를 입력해 주세요.", false); return; }
            try
            {
                PageResponse<UserSummary> page = await FriendApi.SearchUsersAsync(q);
                if (_popup == null || _tab != TabSearch || _searchList == null) return;
                Clear(_searchList);
                float y = 0;
                foreach (UserSummary u in page.Content)
                {
                    UserSummary user = u;
                    Row(_searchList, ref y, Name(user), null, null, ("친구 요청", () => Request(user), null));
                }
                _searchList.sizeDelta = new Vector2(0, -y);
                Status(page.Content.Count == 0 ? $"'{q}'이(가) 들어간 닉네임이 없습니다."
                    : page.TotalElements > page.Content.Count ? $"{page.TotalElements}명 중 {page.Content.Count}명 (더 자세히 입력해 보세요)"
                    : $"{page.Content.Count}명", page.Content.Count > 0);
            }
            catch (ApiException e) { Status(Error(e), false); }
        }

        private async void Request(UserSummary user)
        {
            try
            {
                FriendRequestResult r = await FriendApi.SendRequestAsync(user.UserId);
                Status(r.Status == "ACCEPTED"
                    ? $"{Name(user)} 님도 요청을 보냈었어서 바로 친구가 되었습니다."
                    : $"{Name(user)} 님에게 친구 요청을 보냈습니다.", true);
            }
            catch (ApiException e)
            {
                Status(e.Code == "ALREADY_FRIEND" ? $"{Name(user)} 님은 이미 친구입니다."
                    : e.Code == "REQUEST_ALREADY_EXISTS" ? $"{Name(user)} 님에게 이미 요청을 보냈습니다."
                    : Error(e), false);
            }
        }

        // ================= 대화 =================

        /// <summary>그 사람과의 대화 화면 (같은 팝업 안)</summary>
        public void OpenChat(UserSummary partner)
        {
            if (_popup == null) Build("친구·메시지");
            _partner = partner;
            _lastMessageId = 0;
            ClearBody();
            var back = Button(_body, "← 목록", () => { _partner = null; ShowTab(); }, 22);
            Place(back, new Vector2(0, 1), Vector2.zero, new Vector2(160, 56), new Vector2(0, 1));
            var title = Label(_body, $"{Name(partner)} 님과 대화", 28, TextAnchor.MiddleLeft, bold: true);
            Place(title, new Vector2(0, 1), new Vector2(180, 0), new Vector2(700, 56), new Vector2(0, 1));
            var refresh = Button(_body, "새로고침", () => LoadChat(false), 20);
            Place(refresh, new Vector2(1, 1), Vector2.zero, new Vector2(160, 56), new Vector2(1, 1));

            _chatList = ScrollArea(_body, 76, 130, out _chatScroll);
            _chatInput = Input(_body, "메시지 (Enter로 보내기, 500자까지)", limit: MessageMax, size: 26);
            Place(_chatInput, new Vector2(0, 0), new Vector2(0, 44), new Vector2(PopupW - 80 - 180, 72), new Vector2(0, 0));
            var send = Button(_body, "보내기", Send, 24);
            Place(send, new Vector2(1, 0), new Vector2(0, 44), new Vector2(160, 72), new Vector2(1, 0));
            OnEnter(_chatInput, Send);
            _status = Label(_body, "", 22, TextAnchor.LowerLeft, Good);
            Place(_status, new Vector2(0, 0), Vector2.zero, new Vector2(PopupW - 80, 32), new Vector2(0, 0));
            _chatInput.Select();
            _chatInput.ActivateInputField();
            _pollLeft = PollSeconds;
            LoadChat(true);
        }

        private async void LoadChat(bool scrollToEnd)
        {
            if (_loadingChat || _partner == null) return;
            _loadingChat = true;
            UserSummary partner = _partner;
            try
            {
                PageResponse<MessageItem> page = await MessageApi.GetMessagesAsync(partner.UserId, 50);
                if (_popup == null || _partner != partner || _chatList == null) return;
                long newest = page.Content.Count > 0 ? page.Content.Max(m => m.MessageId) : 0;
                bool changed = newest != _lastMessageId;
                // 새 메시지가 없어도 읽음 표시가 바뀔 수 있어 다시 그린다. 아래에 붙어 있었으면 계속 맨 아래로
                bool atEnd = _chatScroll == null || _chatScroll.verticalNormalizedPosition <= 0.01f;
                _lastMessageId = newest;
                DrawMessages(page.Content, partner);
                if (scrollToEnd || (changed && atEnd)) ScrollToEnd();
                if (changed) RefreshBadge();
            }
            catch (ApiException e) { Status(Error(e), false); }
            finally { _loadingChat = false; }
        }

        private void DrawMessages(List<MessageItem> newestFirst, UserSummary partner)
        {
            Clear(_chatList);
            long me = PlayerSession.Profile?.UserId ?? 0;
            float width = PopupW - 80;
            float y = 0;
            if (newestFirst.Count == 0)
            {
                var empty = Label(_chatList, "아직 주고받은 메시지가 없습니다. 첫 메시지를 보내 보세요.", 24, TextAnchor.MiddleCenter, Muted);
                Place(empty, new Vector2(0, 1), new Vector2(0, -20), new Vector2(width, 60), new Vector2(0, 1));
                _chatList.sizeDelta = new Vector2(0, 100);
                return;
            }
            foreach (MessageItem m in Enumerable.Reverse(newestFirst))
            {
                bool mine = m.SenderId == me;
                // 줄 수를 대강 세서 말풍선 높이를 정한다 (한 줄 약 34자)
                int lines = Mathf.Clamp(Mathf.CeilToInt((m.Content?.Length ?? 0) / 34f), 1, 15)
                            + (m.Content ?? "").Count(c => c == '\n');
                float h = 30 + lines * 32 + 26;
                float bubbleW = Mathf.Min(760, width * 0.7f);
                var bubble = Panel(_chatList, "Message", mine ? (Color)new Color32(0x1c, 0x3a, 0x52, 0xff) : new Color32(0x22, 0x2a, 0x36, 0xff));
                Place(bubble, new Vector2(mine ? 1 : 0, 1), new Vector2(0, y), new Vector2(bubbleW, h), new Vector2(mine ? 1 : 0, 1));
                var text = Label(bubble, m.Content ?? "", 24, mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
                text.supportRichText = false;
                Stretch(text.rectTransform, 14);
                text.rectTransform.offsetMin = new Vector2(16, 30);
                string meta = $"{m.CreatedAt.ToLocalTime():MM.dd HH:mm}" + (mine ? (m.IsRead ? " · 읽음" : " · 안 읽음") : $" · {Name(partner)}");
                var info = Label(bubble, meta, 18, mine ? TextAnchor.LowerRight : TextAnchor.LowerLeft, Muted);
                Stretch(info.rectTransform, 10);
                y -= h + 12;
            }
            _chatList.sizeDelta = new Vector2(0, -y);
        }

        private void ScrollToEnd()
        {
            if (_chatScroll == null) return;
            UnityEngine.Canvas.ForceUpdateCanvases();
            _chatScroll.verticalNormalizedPosition = 0;
        }

        private async void Send()
        {
            if (_chatInput == null || _partner == null) return;
            string text = _chatInput.text.Trim();
            if (text.Length == 0) return;
            if (text.Length > MessageMax) { Status($"메시지는 {MessageMax}자까지입니다.", false); return; }
            UserSummary partner = _partner;
            _chatInput.text = "";
            _chatInput.ActivateInputField();
            try
            {
                await MessageApi.SendAsync(partner.UserId, text);
                if (_partner != partner) return;
                Status("", true);
                LoadChat(true);
            }
            catch (ApiException e)
            {
                if (_chatInput != null && _partner == partner) _chatInput.text = text; // 보내지 못한 내용은 되돌려 둔다
                Status(e.Code == "NOT_FRIEND" ? "친구에게만 메시지를 보낼 수 있습니다." : Error(e), false);
            }
        }
    }
}
