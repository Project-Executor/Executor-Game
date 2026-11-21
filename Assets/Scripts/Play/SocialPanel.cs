using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Executor.Data;
using Executor.Network;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 친구·메시지 창 (시안 PopFriends, messenger.exe). 메인 화면(친구 아이콘)과 플레이 화면(채팅 버튼)이 같은 창을 쓴다.
    /// 왼쪽: 닉네임 찾기 + 메뉴(메시지 · 친구 · 받은 요청 · 보낸 요청). 오른쪽: 고른 메뉴의 목록, 친구 상세(기록 비교 · 채팅 ·
    /// 도전장(업데이트 예정)), 대화. 대화는 열려 있는 동안 몇 초마다 새 메시지를 불러온다 (Tick).
    /// 친구 접속 상태·친구 코드·내 맵 보내기는 MVP에서 뺐다 (Executor-Server docs/requirements.md 3.12)
    /// </summary>
    public class SocialPanel
    {
        public const string TabMessages = "messages", TabFriends = "friends", TabReceived = "received", TabSent = "sent", TabSearch = "search";

        // 창 (시안 left 236, top 60, 820 x 520)
        private const float WinW = 820 * S, WinH = 520 * S, NavW = 260 * S, PadX = 24 * S, PadY = 20 * S;
        private static float BodyH => WinH - WinBorder * 2 - WinTitleH;
        private static float PaneW => WinW - WinBorder * 2 - NavW - PadX * 2;
        private const float PollSeconds = 5f;
        private const int MessageMax = 500;

        private readonly Func<Transform> _root;
        private readonly Action<int> _onBadge;

        /// <summary>창이 뜨거나 닫힐 때 작업 표시줄 탭 (이름 / null)</summary>
        public Action<string> OnWindow;

        /// <summary>친구 상세의 기록 비교: (스테이지 "1-2", 내 최소 턴, 친구 최소 턴). 없으면 표를 숨긴다</summary>
        public Func<UserSummary, Task<List<(string stage, int? me, int? friend)>>> CompareRecords;

        private GameObject _popup;
        private RectTransform _nav, _body;
        private Text _status;
        private string _tab = TabMessages;
        private int _received, _unread;
        private InputField _searchInput;
        private string _lastSearch;

        // 대화 화면
        private UserSummary _partner;
        private RectTransform _chatList;
        private ScrollRect _chatScroll;
        private InputField _chatInput;
        private float _pollLeft;
        /// <summary>
        /// 다음 Tick에 다시 포커스를 줄 입력칸. Enter를 누르면 Unity 입력칸이 스스로 포커스를 놓는데,
        /// 그 처리(onEndEdit) 안에서 바로 다시 잡으면 무시되므로 한 프레임 뒤에 잡는다. 보내기·검색 버튼을 눌렀을 때도 같다
        /// </summary>
        private InputField _refocus;
        private bool _loadingChat;
        private long _lastMessageId;

        /// <param name="popupRoot">창을 붙일 곳 (메뉴 또는 게임 화면의 팝업 자리)</param>
        /// <param name="onBadge">안 읽은 메시지 + 받은 요청 수가 바뀌면 (버튼 숫자 표시용)</param>
        public SocialPanel(Func<Transform> popupRoot, Action<int> onBadge)
        {
            _root = popupRoot;
            _onBadge = onBadge;
        }

        public bool IsOpen => _popup != null;

        /// <summary>창을 연다 (이미 열려 있으면 메뉴만 바꾼다)</summary>
        public void Open(string tab = TabMessages)
        {
            Build();
            if (!PlayerSession.IsOnline)
            {
                Note("친구와 메시지는 로그인하면 쓸 수 있습니다.");
                return;
            }
            _tab = tab;
            _partner = null;
            ShowTab();
        }

        public void Close()
        {
            if (_popup != null) UnityEngine.Object.Destroy(_popup);
            _popup = null;
            _partner = null;
            OnWindow?.Invoke(null);
            RefreshBadge();
        }

        /// <summary>대화 화면이 열려 있으면 PollSeconds마다 새 메시지를 불러온다</summary>
        public void Tick(float deltaTime)
        {
            if (_refocus != null)
            {
                InputField field = _refocus;
                _refocus = null;
                if (field != null && field.gameObject.activeInHierarchy && !field.isFocused)
                {
                    field.Select();
                    field.ActivateInputField();
                }
            }
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
                if (_popup != null) DrawNav();
            }
            catch (ApiException e)
            {
                Debug.LogWarning("[SocialPanel] 알림 수를 불러오지 못했습니다: " + e.Message);
            }
        }

        // ================= 틀 =================

        private void Build()
        {
            if (_popup != null) UnityEngine.Object.Destroy(_popup);
            WindowFrame(_root(), "친구 — messenger.exe", "friends", 236, 60, 820, 520, false, Close, out RectTransform body, out _popup);
            OnWindow?.Invoke("messenger.exe");

            Image nav = Fill(body, "Nav", Night);
            At(nav, 0, 0, NavW - 2 * S, BodyH);
            Image navLine = Fill(body, "NavLine", TrackLine);
            At(navLine, NavW - 2 * S, 0, 2 * S, BodyH);
            _nav = nav.rectTransform;

            // 왼쪽 위: 닉네임으로 찾기
            Text label = Label(_nav, "친구 찾기", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(label, 12 * S, 12 * S, NavW - 24 * S, 16 * S);
            _searchInput = UiKit.Input(_nav, "닉네임 입력", limit: 20, size: (int)(12 * S));
            StyleInput(_searchInput);
            At(_searchInput, 12 * S, 12 * S + 16 * S + 8 * S, NavW - 2 * S - 24 * S - 52 * S - 6 * S, 28 * S);
            PrimaryButton(_nav, "찾기", 12 * S, () => { _refocus = _searchInput; SearchFromNav(); }, out RectTransform go);
            At(go, NavW - 2 * S - 12 * S - 52 * S, 12 * S + 16 * S + 8 * S, 52 * S, 28 * S);
            UiKit.OnEnter(_searchInput, () => { _refocus = _searchInput; SearchFromNav(); });
            Image sep = Fill(_nav, "Line", TrackLine);
            At(sep, 0, 12 * S + 16 * S + 8 * S + 28 * S + 12 * S, NavW - 2 * S, 2 * S);

            _body = Node(body, "Pane");
            At(_body, NavW + PadX, PadY, PaneW, BodyH - PadY * 2);
            DrawNav();
        }

        private const float NavMenuTop = 12 * S + 16 * S + 8 * S + 28 * S + 12 * S + 2 * S + 8 * S;

        /// <summary>왼쪽 메뉴 (메시지 · 친구 · 받은 요청 · 보낸 요청). 지금 메뉴는 하늘색</summary>
        private void DrawNav()
        {
            if (_nav == null) return;
            Transform old = _nav.Find("Menu");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            RectTransform menu = Node(_nav, "Menu");
            At(menu, 0, NavMenuTop, NavW - 2 * S, BodyH - NavMenuTop);
            if (!PlayerSession.IsOnline) return;
            float y = 0;
            foreach (var (tab, label, count) in new[]
                     {
                         (TabMessages, "메시지", _unread), (TabFriends, "친구", 0), (TabReceived, "받은 요청", _received), (TabSent, "보낸 요청", 0),
                     })
            {
                string t = tab;
                bool current = _tab == t && _partner == null;
                const float h = 40 * S;
                RectTransform row = Node(menu, "Tab:" + label);
                At(row, 0, y, NavW - 2 * S, h);
                if (current)
                {
                    Image bg = Fill(row, "Bg", Hex(0x22d3ee, 0.14f));
                    Inset(bg, 0, 0, 0, 0);
                    Image bar = Fill(row, "Bar", Cyan);
                    At(bar, 0, 0, 2 * S, h);
                }
                else
                {
                    HoverArea(row, "Hover", Hex(0x22d3ee, 0.10f), () => { _tab = t; _partner = null; ShowTab(); }, out Image area);
                    Inset(area, 0, 0, 0, 0);
                }
                Text text = Label(row, label, 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
                Inset(text, 12 * S, 0, 40 * S, 0);
                if (count > 0)
                {
                    Text c = Label(row, count.ToString(), 12 * S, Red, TextAnchor.MiddleRight, UiKit.Mono);
                    Inset(c, 0, 0, 12 * S, 0);
                }
                y += h + 2 * S;
            }
        }

        private static void StyleInput(InputField field)
        {
            var image = field.GetComponent<Image>();
            if (image != null) image.color = Ink;
            var outline = field.GetComponent<Outline>();
            if (outline != null) outline.effectColor = TrackLine;
        }

        private void ClearBody()
        {
            UiKit.Clear(_body);
            _chatList = null;
            _chatInput = null;
            _status = null;
        }

        private void Note(string text)
        {
            Text t = Label(_body != null ? _body : _nav, text, 12 * S, Muted, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            Inset(t, 0, 0, 0, 0);
        }

        /// <summary>아래쪽 안내 줄 (성공은 초록, 실패는 빨강)</summary>
        private void Status(string text, bool good)
        {
            if (_status == null) return;
            _status.text = text ?? "";
            _status.color = good ? Green : Red;
        }

        private static string Error(ApiException e) => e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message;

        private static string Name(UserSummary u) => string.IsNullOrEmpty(u?.Nickname) ? "(닉네임 없음)" : u.Nickname;

        /// <summary>세로 스크롤 목록 (content에 위에서부터 At으로 넣고, 끝에 content.sizeDelta.y를 높이로)</summary>
        private static RectTransform ScrollArea(RectTransform parent, float top, float bottom, out ScrollRect scroll)
        {
            RectTransform viewport = Node(parent, "Viewport");
            Inset(viewport, 0, top, 0, bottom);
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

        /// <summary>오른쪽 머리: 큰 제목 + 아래 안내 줄 자리</summary>
        private void Heading(string title)
        {
            Text t = Label(_body, title, 24 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(t, 0, 0, PaneW, 28 * S);
            _status = Label(_body, "", 12 * S, Green, TextAnchor.MiddleLeft);
            At(_status, 0, BodyH - PadY * 2 - 16 * S, PaneW, 16 * S);
        }

        // ================= 메뉴 =================

        private void ShowTab()
        {
            ClearBody();
            DrawNav();
            string title = _tab switch
            {
                TabFriends => "친구",
                TabReceived => "받은 요청",
                TabSent => "보낸 요청",
                TabSearch => "친구 찾기",
                _ => "메시지",
            };
            Heading(title);
            Text loading = Label(_body, "불러오는 중…", 12 * S, Muted, TextAnchor.MiddleCenter);
            Inset(loading, 0, 0, 0, 0);
            switch (_tab)
            {
                case TabFriends: LoadFriends(loading); break;
                case TabReceived: LoadRequests(loading, true); break;
                case TabSent: LoadRequests(loading, false); break;
                case TabSearch: Destroy(loading); DrawSearchResults(); break;
                default: LoadRooms(loading); break;
            }
        }

        private static void Destroy(Component c)
        {
            if (c != null) UnityEngine.Object.Destroy(c.gameObject);
        }

        private const float RowH = 10 * S + 16 * S + 2 * S + 16 * S + 10 * S;

        /// <summary>
        /// 목록 한 줄: 아바타 + 이름·설명, 오른쪽 버튼들 (오른쪽부터). 버튼 색이 null이면 주 버튼(채움), 아니면 테두리 버튼.
        /// onRow가 있으면 줄 전체를 누를 수 있다
        /// </summary>
        private static RectTransform Row(RectTransform list, ref float y, string title, string sub, Color? titleColor, Action onRow,
            params (string label, Action onClick, Color? color)[] buttons)
        {
            RectTransform row = Node(list, "Row");
            At(row, 0, y, PaneW, RowH);
            Image bg = Fill(row, "Bg", Color.white);
            Inset(bg, 0, 0, 0, 0);
            if (onRow != null) Clickable(bg, onRow, Night, Hex(0x1e1942));
            else bg.color = Night;
            Image line = Fill(row, "Line", TrackLine);
            At(line, 0, RowH - 1 * S, PaneW, 1 * S);
            Avatar(row, 12 * S, (RowH - 28 * S) / 2, 28 * S);

            float right = 12 * S;
            foreach (var (label, onClick, color) in buttons)
            {
                Text measure = Label(row, label, 12 * S, Fg, TextAnchor.MiddleCenter, bold: true);
                float bw = measure.preferredWidth + 24 * S;
                UnityEngine.Object.Destroy(measure.gameObject);
                RectTransform b;
                if (color == null) PrimaryButton(row, label, 12 * S, onClick, out b);
                else GhostButton(row, label, 12 * S, onClick, out b);
                AtRight(b, right, (RowH - 28 * S) / 2, bw, 28 * S);
                right += bw + 8 * S;
            }
            float textLeft = 12 * S + 28 * S + 10 * S;
            Text t = Label(row, title, 12 * S, titleColor ?? Fg, TextAnchor.MiddleLeft, bold: true);
            At(t, textLeft, 10 * S, PaneW - textLeft - right, 16 * S);
            if (!string.IsNullOrEmpty(sub))
            {
                Text s = Label(row, sub, 12 * S, Muted, TextAnchor.MiddleLeft);
                s.supportRichText = false;
                At(s, textLeft, 10 * S + 18 * S, PaneW - textLeft - right, 16 * S);
            }
            y += RowH;
            return row;
        }

        /// <summary>사람 자리 (얼굴 그림 대신 회색 상자)</summary>
        private static void Avatar(Transform parent, float left, float top, float size)
        {
            RectTransform a = Node(parent, "Avatar");
            At(a, left, top, size, size);
            Image head = Fill(a, "Head", Hex(0x2d3142));
            Inset(head, 0, 0, 0, 0);
            Image body = Fill(a, "Body", Dim);
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = new Vector2(1, 0);
            body.rectTransform.offsetMin = Vector2.zero;
            body.rectTransform.offsetMax = new Vector2(0, size / 3);
        }

        private RectTransform ListArea(out ScrollRect scroll) => ScrollArea(_body, 28 * S + 12 * S, 16 * S + 8 * S, out scroll);

        private async void LoadRooms(Text loading)
        {
            try
            {
                List<MessageRoom> rooms = await MessageApi.GetRoomsAsync();
                if (_popup == null || _tab != TabMessages || _partner != null) return;
                Destroy(loading);
                if (rooms.Count == 0) { Note("아직 대화가 없습니다.\n친구를 골라 채팅을 시작해 보세요."); return; }
                var list = ListArea(out _);
                float y = 0;
                foreach (MessageRoom r in rooms)
                {
                    UserSummary partner = r.Partner;
                    string title = Name(partner) + (r.UnreadCount > 0 ? $"  <color=#ff4d5e>새 메시지 {r.UnreadCount}</color>" : "");
                    Row(list, ref y, title, $"{r.LastMessage}  · {r.LastMessageAt.ToLocalTime():MM.dd HH:mm}", null,
                        () => OpenChat(partner), ("열기", () => OpenChat(partner), null));
                }
                list.sizeDelta = new Vector2(0, y);
            }
            catch (ApiException e) { if (loading != null) loading.text = Error(e); }
        }

        private async void LoadFriends(Text loading)
        {
            try
            {
                List<FriendResponse> friends = await FriendApi.GetFriendsAsync();
                if (_popup == null || _tab != TabFriends || _partner != null) return;
                Destroy(loading);
                if (friends.Count == 0) { Note("아직 친구가 없습니다.\n왼쪽 위에서 닉네임으로 찾아 요청을 보내 보세요."); return; }
                var list = ListArea(out _);
                float y = 0;
                foreach (FriendResponse f in friends)
                {
                    FriendResponse friend = f;
                    Row(list, ref y, Name(f.User), $"{f.Since.ToLocalTime():yyyy.MM.dd}부터 친구", null, () => ShowFriend(friend),
                        ("보기", () => ShowFriend(friend), null));
                }
                list.sizeDelta = new Vector2(0, y);
                Status($"친구 {friends.Count}명", true);
            }
            catch (ApiException e) { if (loading != null) loading.text = Error(e); }
        }

        // ================= 친구 상세 =================

        /// <summary>친구 상세: 이름, 친구가 된 날, 기록 비교 표, 채팅 · 친구 끊기 · 도전장(업데이트 예정)</summary>
        private void ShowFriend(FriendResponse friend)
        {
            UserSummary user = friend.User;
            ClearBody();
            DrawNav();
            Avatar(_body, 0, 0, 48 * S);
            Text name = Label(_body, Name(user), 24 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(name, 48 * S + 14 * S, 0, PaneW - 62 * S, 28 * S);
            Text since = Label(_body, $"{friend.Since.ToLocalTime():yyyy.MM.dd}부터 친구", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(since, 48 * S + 14 * S, 32 * S, PaneW - 62 * S, 16 * S);
            float bottom = BodyH - PadY * 2;
            _status = Label(_body, "", 12 * S, Green, TextAnchor.MiddleLeft);
            At(_status, 0, bottom - 36 * S - 8 * S - 16 * S, PaneW, 16 * S);

            Text head = Label(_body, "기록 비교 <color=#8b98b8>· 최소 턴, 낮을수록 좋음</color>", 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(head, 0, 48 * S + 16 * S, PaneW, 16 * S);
            RectTransform table = Node(_body, "Compare");
            At(table, 0, 48 * S + 16 * S + 16 * S + 6 * S, PaneW, 200 * S);
            Text loading = Label(table, "기록을 불러오는 중…", 12 * S, Muted, TextAnchor.UpperLeft);
            At(loading, 8 * S, 8 * S, PaneW, 16 * S);
            LoadCompare(user, table, loading);

            // 아래 버튼: 채팅 · 친구 끊기 · 도전장 (업데이트 예정)
            float by = bottom - 36 * S;
            GhostButton(_body, "채팅", 12 * S, () => OpenChat(user), out RectTransform chat);
            At(chat, 0, by, 90 * S, 36 * S);
            GhostButton(_body, "친구 끊기", 12 * S, () => ConfirmUnfriend(user), out RectTransform unfriend);
            At(unfriend, 90 * S + 10 * S, by, 100 * S, 36 * S);
            Button challenge = PrimaryButton(_body, "도전장 보내기 (업데이트 예정)", 12 * S, null, out RectTransform ch);
            challenge.interactable = false;
            At(ch, 200 * S + 10 * S, by, PaneW - 210 * S, 36 * S);
        }

        private async void LoadCompare(UserSummary user, RectTransform table, Text loading)
        {
            if (CompareRecords == null) { loading.text = "비교할 기록이 없습니다."; return; }
            try
            {
                List<(string stage, int? me, int? friend)> rows = await CompareRecords(user);
                if (table == null || _popup == null) return;
                Destroy(loading);
                if (rows.Count == 0)
                {
                    Text none = Label(table, "둘 다 기록이 있는 스테이지가 아직 없습니다.", 12 * S, Muted, TextAnchor.UpperLeft);
                    At(none, 8 * S, 8 * S, PaneW, 16 * S);
                    return;
                }
                const float rowH = 32 * S;
                Image bg = Fill(table, "Bg", Ink);
                At(bg, 0, 0, PaneW, rowH * (rows.Count + 1));
                float[] cols = { 8 * S, PaneW * 0.4f, PaneW * 0.7f };
                string[] heads = { "스테이지", "나", Name(user) };
                for (int c = 0; c < 3; c++)
                {
                    Text h = Label(table, heads[c], 12 * S, Muted, TextAnchor.MiddleLeft);
                    At(h, cols[c], 0, PaneW * 0.3f, rowH);
                }
                Image headLine = Fill(table, "HeadLine", TrackLine);
                At(headLine, 0, rowH - 2 * S, PaneW, 2 * S);
                for (int i = 0; i < rows.Count; i++)
                {
                    var (stage, me, friend) = rows[i];
                    float y = rowH * (i + 1);
                    Text s = Label(table, stage, 12 * S, Muted, TextAnchor.MiddleLeft);
                    At(s, cols[0], y, PaneW * 0.3f, rowH);
                    bool friendBetter = friend != null && (me == null || friend < me);
                    bool meBetter = me != null && (friend == null || me < friend);
                    Text m = Label(table, me?.ToString() ?? "—", 12 * S, me == null ? Dim : meBetter ? Green : Fg, TextAnchor.MiddleLeft, UiKit.Mono);
                    At(m, cols[1], y, PaneW * 0.3f, rowH);
                    Text f = Label(table, friend?.ToString() ?? "—", 12 * S, friend == null ? Dim : friendBetter ? Green : Fg, TextAnchor.MiddleLeft, UiKit.Mono);
                    At(f, cols[2], y, PaneW * 0.3f, rowH);
                    Image line = Fill(table, "Line", TrackLine);
                    At(line, 0, y + rowH - 1 * S, PaneW, 1 * S);
                }
            }
            catch (ApiException e) { if (loading != null) loading.text = Error(e); }
        }

        private void ConfirmUnfriend(UserSummary user)
        {
            GameObject dialog = null;
            WindowFrame(_root(), "시스템", null, (1280 - 460) / 2f, 250, 460, 220, true, () => UnityEngine.Object.Destroy(dialog),
                out RectTransform body, out dialog);
            float bodyH = 220 * S - WinBorder * 2 - WinTitleH;
            Text head = Label(body, $"{Name(user)} 님과 친구를 끊을까요?", 24 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(head, 24 * S, 24 * S, 412 * S, 28 * S);
            Text desc = Label(body, "지난 대화는 남고, 다시 요청할 수 있습니다.", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(desc, 24 * S, 24 * S + 28 * S + 8 * S, 412 * S, 16 * S);
            GhostButton(body, "아니요", 12 * S, () => UnityEngine.Object.Destroy(dialog), out RectTransform no);
            AtRight(no, 24 * S + 120 * S + 10 * S, bodyH - 20 * S - 40 * S, 120 * S, 40 * S);
            PrimaryButton(body, "끊기", 12 * S, async () =>
            {
                UnityEngine.Object.Destroy(dialog);
                try
                {
                    await FriendApi.DeleteFriendAsync(user.UserId);
                    if (_popup == null) return;
                    _tab = TabFriends;
                    ShowTab();
                    Status($"{Name(user)} 님과 친구를 끊었습니다.", true);
                }
                catch (ApiException e) { Status(Error(e), false); }
            }, out RectTransform yes);
            AtRight(yes, 24 * S, bodyH - 20 * S - 40 * S, 120 * S, 40 * S);
        }

        // ================= 요청 =================

        private async void LoadRequests(Text loading, bool received)
        {
            string tab = received ? TabReceived : TabSent;
            try
            {
                List<FriendRequestItem> items = received ? await FriendApi.GetReceivedAsync() : await FriendApi.GetSentAsync();
                if (_popup == null || _tab != tab || _partner != null) return;
                Destroy(loading);
                if (received) { _received = items.Count; _onBadge?.Invoke(_unread + _received); DrawNav(); }
                if (items.Count == 0) { Note(received ? "받은 친구 요청이 없습니다." : "대기 중인 보낸 요청이 없습니다."); return; }
                var list = ListArea(out _);
                float y = 0;
                foreach (FriendRequestItem r in items)
                {
                    FriendRequestItem req = r;
                    string sub = $"{r.CreatedAt.ToLocalTime():MM.dd HH:mm}에 {(received ? "받음" : "보냄")}";
                    if (received)
                        Row(list, ref y, Name(r.User), sub, null, null,
                            ("수락", () => Answer(req, true), null),
                            ("거절", () => Answer(req, false), Red));
                    else
                        Row(list, ref y, Name(r.User), sub, null, null, ("취소", () => Cancel(req), Red));
                }
                list.sizeDelta = new Vector2(0, y);
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

        private List<UserSummary> _searchResults = new List<UserSummary>();
        private string _searchNote;

        private void SearchFromNav()
        {
            if (!PlayerSession.IsOnline || _searchInput == null) return;
            _tab = TabSearch;
            _partner = null;
            Search(_searchInput.text);
        }

        private void DrawSearchResults()
        {
            RectTransform list = ListArea(out _);
            float y = 0;
            foreach (UserSummary u in _searchResults)
            {
                UserSummary user = u;
                Row(list, ref y, Name(user), null, null, null, ("친구 요청", () => Request(user), null));
            }
            list.sizeDelta = new Vector2(0, y);
            if (_searchResults.Count == 0 && _lastSearch == null) Note("왼쪽 위에 닉네임 일부를 입력하고 찾기를 누르세요.");
            if (_searchNote != null) Status(_searchNote, _searchResults.Count > 0 || _searchNote.StartsWith("내 닉네임"));
        }

        private async void Search(string text)
        {
            string q = (text ?? "").Trim();
            if (q.Length == 0)
            {
                ShowTab();
                Status("검색어를 입력해 주세요.", false);
                return;
            }
            try
            {
                PageResponse<UserSummary> page = await FriendApi.SearchUsersAsync(q);
                if (_popup == null || _tab != TabSearch) return;
                _lastSearch = q;
                _searchResults = page.Content;
                // 검색 결과에는 나 자신이 나오지 않는다 (서버가 뺀다). 내 닉네임을 찾았을 때 "없다"고 하지 않도록 따로 알린다
                string mine = PlayerSession.Nickname ?? "";
                bool isMe = mine.Length > 0 && string.Equals(mine, q, StringComparison.OrdinalIgnoreCase);
                bool matchesMe = mine.Length > 0 && mine.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                _searchNote = page.Content.Count == 0 && isMe ? "내 닉네임입니다. 친구의 닉네임을 입력해 주세요."
                    : page.Content.Count == 0 && matchesMe ? $"'{q}'이(가) 들어간 다른 사람의 닉네임이 없습니다. (나는 검색 결과에 나오지 않습니다)"
                    : page.Content.Count == 0 ? $"'{q}'이(가) 들어간 닉네임이 없습니다."
                    : page.TotalElements > page.Content.Count ? $"{page.TotalElements}명 중 {page.Content.Count}명 (더 자세히 입력해 보세요)"
                    : $"{page.Content.Count}명";
                ShowTab();
            }
            catch (ApiException e)
            {
                ShowTab();
                Status(Error(e), false);
            }
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

        /// <summary>그 사람과의 대화 화면 (같은 창 오른쪽)</summary>
        public void OpenChat(UserSummary partner)
        {
            if (_popup == null) Build();
            _partner = partner;
            _lastMessageId = 0;
            ClearBody();
            DrawNav();
            GhostButton(_body, "← 목록", 12 * S, () => { _partner = null; ShowTab(); }, out RectTransform back);
            At(back, 0, 0, 70 * S, 28 * S);
            Text title = Label(_body, Name(partner), 24 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(title, 70 * S + 14 * S, 0, PaneW - 84 * S - 150 * S, 28 * S);
            GhostButton(_body, "새로고침", 12 * S, () => LoadChat(false), out RectTransform refresh);
            AtRight(refresh, 0, 0, 80 * S, 28 * S);
            GhostButton(_body, "신고", 12 * S, () => ReportPanel.Open(_root(), new ReportPanel.Target
            {
                UserId = partner.UserId,
                Label = $"사용자 {Name(partner)}",
                PreferCategory = "기타",
                Title = "부적절한 메시지",
            }), out RectTransform report);
            AtRight(report, 80 * S + 8 * S, 0, 56 * S, 28 * S);

            float bottom = BodyH - PadY * 2;
            _chatList = ScrollArea(_body, 28 * S + 12 * S, 36 * S + 8 * S + 16 * S + 8 * S, out _chatScroll);
            _chatInput = UiKit.Input(_body, "메시지 (Enter로 보내기, 500자까지)", limit: MessageMax, size: (int)(12 * S));
            StyleInput(_chatInput);
            At(_chatInput, 0, bottom - 16 * S - 8 * S - 36 * S, PaneW - 80 * S - 8 * S, 36 * S);
            PrimaryButton(_body, "보내기", 12 * S, Send, out RectTransform send);
            At(send, PaneW - 80 * S, bottom - 16 * S - 8 * S - 36 * S, 80 * S, 36 * S);
            UiKit.OnEnter(_chatInput, Send);
            _status = Label(_body, "", 12 * S, Green, TextAnchor.MiddleLeft);
            At(_status, 0, bottom - 16 * S, PaneW, 16 * S);
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
            UiKit.Clear(_chatList);
            long me = PlayerSession.Profile?.UserId ?? 0;
            float y = 0;
            if (newestFirst.Count == 0)
            {
                Text empty = Label(_chatList, "아직 주고받은 메시지가 없습니다. 첫 메시지를 보내 보세요.", 12 * S, Muted, TextAnchor.MiddleCenter);
                At(empty, 0, 20 * S, PaneW, 24 * S);
                _chatList.sizeDelta = new Vector2(0, 60 * S);
                return;
            }
            float bubbleW = PaneW * 0.72f;
            foreach (MessageItem m in Enumerable.Reverse(newestFirst))
            {
                bool mine = m.SenderId == me;
                Text text = Label(_chatList, m.Content ?? "", 12 * S, Fg, mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
                text.supportRichText = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.lineSpacing = 1.3f;
                text.rectTransform.sizeDelta = new Vector2(bubbleW - 24 * S, 10);
                float textH = text.preferredHeight;
                float h = 10 * S + textH + 6 * S + 16 * S + 8 * S;
                float x = mine ? PaneW - bubbleW : 0;
                Image bubble = Fill(_chatList, "Message", mine ? Hex(0x22d3ee, 0.14f) : Night);
                At(bubble, x, y, bubbleW, h);
                Image bar = Fill(bubble.transform, "Bar", mine ? Cyan : TrackLine);
                At(bar, mine ? bubbleW - 2 * S : 0, 0, 2 * S, h);
                text.transform.SetParent(bubble.transform, false);
                At(text, 12 * S, 10 * S, bubbleW - 24 * S, textH);
                string meta = $"{m.CreatedAt.ToLocalTime():MM.dd HH:mm}" + (mine ? (m.IsRead ? " · 읽음" : " · 안 읽음") : $" · {Name(partner)}");
                Text info = Label(bubble.transform, meta, 12 * S, Dim, mine ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                At(info, 12 * S, 10 * S + textH + 6 * S, bubbleW - 24 * S, 16 * S);
                y += h + 8 * S;
            }
            _chatList.sizeDelta = new Vector2(0, y);
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
            _refocus = _chatInput; // 보내든 못 보내든 입력칸에 계속 쓸 수 있게
            string text = _chatInput.text.Trim();
            if (text.Length == 0) return;
            if (text.Length > MessageMax) { Status($"메시지는 {MessageMax}자까지입니다.", false); return; }
            UserSummary partner = _partner;
            _chatInput.text = "";
            try
            {
                await MessageApi.SendAsync(partner.UserId, text);
                if (_partner != partner) return;
                Status("", true);
                LoadChat(true);
            }
            catch (ApiException e)
            {
                if (_chatInput != null && _partner == partner)
                {
                    _chatInput.text = text; // 보내지 못한 내용은 되돌려 둔다
                    _refocus = _chatInput;
                }
                Status(e.Code switch
                {
                    "NOT_FRIEND" => "친구에게만 메시지를 보낼 수 있습니다.",
                    "MESSAGE_SPAM" => "메시지를 너무 빠르게 보내고 있습니다. 잠시 후 다시 보내 주세요.",
                    _ => Error(e),
                }, false);
            }
        }
    }
}
