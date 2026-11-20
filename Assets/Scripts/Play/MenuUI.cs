using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.UiKit;
using Object = UnityEngine.Object;

namespace Executor.Play
{
    /// <summary>
    /// 메뉴 화면: 시작(index) → 메인(main) → 스테이지 선택(stage). 와이어프레임 배치를 기준으로
    /// 여백 48px, 설정 버튼 120x120, 버튼 간격 24px로 맞췄다.
    /// 팝업(로그인, 설정, 프로필, 진행 상황, 안내)도 여기서 띄운다.
    /// </summary>
    public partial class MenuUI : MonoBehaviour
    {
        private const float Margin = 48;
        private const float Gap = 24;
        private static readonly Vector2 SettingsSize = new Vector2(120, 120);

        private RectTransform _root;
        private RectTransform _popupRoot;
        private GameObject _title, _main, _stage;
        private Text _titleAccount, _titleHint;
        private Button _titleLogin, _titleLogout, _titleOffline, _titleTouch;

        public bool HasPopup => _popupRoot.childCount > 0;

        /// <summary>팝업을 붙이는 자리 (친구·메시지 팝업이 쓴다)</summary>
        public Transform PopupRoot => _popupRoot;

        public void Build()
        {
            _root = Canvas(transform, "MenuCanvas", 0);
            _title = BuildTitle();
            _main = BuildMain();
            _stage = BuildStage();
            _popupRoot = new GameObject("Popups", typeof(RectTransform)).GetComponent<RectTransform>();
            _popupRoot.SetParent(_root, false);
            Stretch(_popupRoot);
            HideAll();
        }

        public void HideAll()
        {
            _title.SetActive(false);
            _main.SetActive(false);
            _stage.SetActive(false);
            ClosePopups();
        }

        public void ClosePopups() => Clear(_popupRoot);

        /// <summary>
        /// 일러스트를 parent 영역에 꽉 채워 깐다 (비율 유지, 넘치는 부분은 잘림). parent의 첫 자식이라 다른 요소 뒤에 그려진다.
        /// sprite가 null이면 아무것도 하지 않는다
        /// </summary>
        private static void Cover(RectTransform parent, Sprite sprite)
        {
            if (sprite == null) return;
            parent.gameObject.AddComponent<RectMask2D>();
            var go = new GameObject("Illustration", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        private static Button SettingsButton(Transform parent, Action onClick)
        {
            var b = Button(parent, "설정", onClick, 26);
            Place(b, new Vector2(1, 1), new Vector2(-Margin, -Margin), SettingsSize, new Vector2(1, 1));
            return b;
        }

        // ================= 시작 화면 =================
        public Action OnTitleStart, OnTitleLogin, OnTitleLogout, OnTitleOffline, OnSettings;

        /// <summary>
        /// ready=false: 가운데 로그인 버튼 + 왼쪽 아래 오프라인 테스트.
        /// ready=true (로그인 또는 오프라인): "화면을 클릭해 게임을 시작하세요" + 왼쪽 아래 계정·로그아웃
        /// </summary>
        public void ShowTitle(bool ready, string account, string logoutLabel)
        {
            HideAll();
            _title.SetActive(true);
            _titleLogin.gameObject.SetActive(!ready);
            _titleOffline.gameObject.SetActive(!ready);
            _titleHint.gameObject.SetActive(ready);
            _titleTouch.enabled = ready;
            _titleAccount.gameObject.SetActive(ready);
            _titleLogout.gameObject.SetActive(ready);
            _titleAccount.text = account;
            SetButtonText(_titleLogout, logoutLabel);
        }

        private GameObject BuildTitle()
        {
            var screen = Panel(_root, "Title", Background);
            Stretch(screen);
            Cover(screen, ArtCatalog.Illustration("title"));
            // 준비되면 화면 아무 데나 클릭해서 시작 (다른 버튼보다 뒤에 깔린 투명 버튼)
            _titleTouch = screen.gameObject.AddComponent<Button>();
            _titleTouch.transition = Selectable.Transition.None;
            _titleTouch.onClick.AddListener(() => OnTitleStart?.Invoke());

            var logo = Panel(screen, "Logo", new Color32(0x16, 0x22, 0x32, 0xff), Line);
            Place(logo, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(760, 300));
            var logoText = Label(logo, "EXECUTOR\n집행자", 72, TextAnchor.MiddleCenter, bold: true);
            Stretch(logoText.rectTransform);
            // 로고 이미지가 있으면 임시 상자 대신 쓴다 (비율 유지)
            Sprite logoArt = ArtCatalog.Illustration("title_logo");
            if (logoArt != null)
            {
                var logoImage = logo.GetComponent<Image>();
                logoImage.sprite = logoArt;
                logoImage.color = Color.white;
                logoImage.preserveAspect = true;
                logoImage.raycastTarget = false;
                logoText.gameObject.SetActive(false);
                var outline = logo.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
            }

            _titleLogin = Button(screen, "로그인", () => OnTitleLogin?.Invoke(), 34);
            Place(_titleLogin, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(380, 96));

            // 화면 아래쪽 (배경 그림 맨 아래 "made by" 글씨보다 위). 흰색, 깜빡임은 Update에서 투명도만 바꾼다
            _titleHint = Label(screen, "화면을 클릭해 게임을 시작하세요", 32, TextAnchor.MiddleCenter, Color.white);
            Place(_titleHint, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(1000, 60));

            SettingsButton(screen, () => OnSettings?.Invoke());

            _titleOffline = Button(screen, "오프라인 테스트", () => OnTitleOffline?.Invoke(), 24, new Color32(0x22, 0x26, 0x2e, 0xff));
            Place(_titleOffline, new Vector2(0, 0), new Vector2(Margin, Margin), new Vector2(260, 64), new Vector2(0, 0));

            _titleLogout = Button(screen, "로그아웃", () => OnTitleLogout?.Invoke(), 24);
            Place(_titleLogout, new Vector2(0, 0), new Vector2(Margin, Margin), new Vector2(260, 64), new Vector2(0, 0));
            _titleAccount = Label(screen, "", 26, TextAnchor.MiddleLeft, Muted);
            Place(_titleAccount, new Vector2(0, 0), new Vector2(Margin, Margin + 64 + 12), new Vector2(800, 40), new Vector2(0, 0));
            return screen.gameObject;
        }

        private void Update()
        {
            if (_title != null && _title.activeSelf && _titleHint.gameObject.activeSelf)
            {
                var c = _titleHint.color;
                c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f));
                _titleHint.color = c;
            }
        }

        // ================= 팝업 =================
        public void ShowMessage(string title, string text)
        {
            Popup(_popupRoot, title, new Vector2(760, 360), out RectTransform body);
            var t = Label(body, text, 28, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
        }

        /// <summary>메일함 팝업: 최신순 목록(누르면 자세히) + 모두 받기</summary>
        public void ShowMailbox(IReadOnlyList<Data.MailResponse> mails, Action<Data.MailResponse> onOpen, Action onClaimAll)
        {
            int unclaimed = mails.Count(m => !m.Claimed);
            Popup(_popupRoot, $"메일함 · 받지 않은 메일 {unclaimed}통", new Vector2(1100, 820), out RectTransform body);
            if (mails.Count == 0)
            {
                var empty = Label(body, "메일이 없습니다.", 28, TextAnchor.MiddleCenter, Muted);
                Stretch(empty.rectTransform);
                return;
            }
            float y = 0;
            foreach (var m in mails.Take(6))
            {
                var mail = m;
                string state = m.Claimed ? "받음" : "받기 전";
                string expiry = m.ExpiresAt == null ? "무기한" : $"~{m.ExpiresAt.Value.ToLocalTime():MM.dd HH:mm}";
                var b = Button(body, $"{m.Title}\n{m.RewardText} · {expiry} · {state}", () => onOpen(mail), 22,
                    m.Claimed ? new Color32(0x1a, 0x1e, 0x25, 0xff) : (Color32?)null);
                var text = b.GetComponentInChildren<Text>();
                text.alignment = TextAnchor.MiddleLeft;
                text.rectTransform.offsetMin = new Vector2(24, 0);
                if (m.Claimed) text.color = Muted;
                Place(b, new Vector2(0, 1), new Vector2(0, y), new Vector2(1020, 88), new Vector2(0, 1));
                y -= 98;
            }
            if (mails.Count > 6)
            {
                var more = Label(body, $"외 {mails.Count - 6}통 (최근 6통만 표시, 모두 받기는 전부 받습니다)", 20, TextAnchor.UpperLeft, Muted);
                Place(more, new Vector2(0, 1), new Vector2(0, y - 4), new Vector2(1020, 32), new Vector2(0, 1));
            }
            if (unclaimed > 0)
            {
                var all = Button(body, $"모두 받기 ({unclaimed}통)", () => onClaimAll(), 26);
                Place(all, new Vector2(1, 0), Vector2.zero, new Vector2(300, 72), new Vector2(1, 0));
            }
        }

        /// <summary>
        /// 상점 팝업. 위에 종류 탭(전체·스킨·테마), 아래에 아이템 줄(최대 7개). 줄마다 가격·보유와 구매 버튼.
        /// 이미 가졌으면 "보유 중"
        /// </summary>
        public void ShowShop(IReadOnlyList<Data.ShopItem> items, int coin, string filter, Action<string> onFilter,
            Action<Data.ShopItem> onBuy)
        {
            Popup(_popupRoot, $"상점 · 보유 코인 {coin}", new Vector2(1240, 880), out RectTransform body);
            float x = 0;
            foreach (var (type, label) in new (string, string)[] { (null, "전체"), (Data.ItemTypes.Skin, "스킨"), (Data.ItemTypes.Theme, "테마") })
            {
                string t = type;
                bool active = t == filter;
                var tab = Button(body, label, () => onFilter(t), 22, active ? (Color?)Line : null);
                Place(tab, new Vector2(0, 1), new Vector2(x, 0), new Vector2(150, 56), new Vector2(0, 1));
                x += 160;
            }
            if (items.Count == 0)
            {
                var empty = Label(body, "판매 중인 아이템이 없습니다.", 28, TextAnchor.MiddleCenter, Muted);
                Stretch(empty.rectTransform);
                return;
            }
            float y = -76;
            foreach (var it in items.Take(7))
            {
                var item = it;
                var row = Panel(body, "Item", new Color32(0x16, 0x1e, 0x2a, 0xff), Line);
                Place(row, new Vector2(0, 1), new Vector2(0, y), new Vector2(1160, 92), new Vector2(0, 1));
                string owned = it.Owned ? " · 보유 중" : "";
                var name = Label(row, $"[{Data.ItemTypes.Label(it.ItemType)}] {it.Name}   {it.Price} 코인{owned}", 24, TextAnchor.UpperLeft, bold: true);
                Stretch(name.rectTransform, 14);
                name.rectTransform.offsetMax = new Vector2(-300, -12);
                var desc = Label(row, it.Description ?? "", 20, TextAnchor.LowerLeft, Muted);
                Stretch(desc.rectTransform, 14);
                desc.rectTransform.offsetMax = new Vector2(-300, -14);
                desc.horizontalOverflow = HorizontalWrapMode.Overflow;

                var buy = Button(row, it.Owned ? "보유 중" : "구매", () => { if (!item.Owned) onBuy(item); }, 24,
                    it.Owned ? new Color32(0x22, 0x26, 0x2e, 0xff) : (Color32?)null);
                buy.interactable = !it.Owned;
                Place(buy, new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(170, 64), new Vector2(1, 0.5f));
                y -= 102;
            }
            if (items.Count > 7)
            {
                var more = Label(body, $"외 {items.Count - 7}개 (종류 탭으로 나눠 보세요)", 20, TextAnchor.UpperLeft, Muted);
                Place(more, new Vector2(0, 1), new Vector2(0, y - 4), new Vector2(1160, 32), new Vector2(0, 1));
            }
        }

        /// <summary>프로필 화면에서 쓰는 동작. 문자열 결과는 그 줄 아래에 보여 줄 안내 (성공·실패 메시지)</summary>
        public class ProfileActions
        {
            public Func<string, Task<string>> CheckNickname;
            public Func<string, Task<string>> SaveNickname;
            public Func<string, Task<string>> SaveComment;
            /// <summary>(스킨 ID, 테마 ID) 장착. null이면 기본</summary>
            public Action<long?, long?> Equip;
        }

        /// <summary>
        /// 프로필 팝업: 정보·기록, 닉네임(중복 확인·변경), 자기소개, 스킨·테마 장착(가진 것 중에서, 기본으로 되돌리기).
        /// </summary>
        public void ShowProfileEditor(Data.MyProfileResponse p, Data.ProfileDetailResponse stats,
            IReadOnlyList<Data.MyItem> items, ProfileActions actions)
        {
            Popup(_popupRoot, "프로필", new Vector2(1240, 920), out RectTransform body);
            string record = stats == null ? "" : $" · 공식 스테이지 클리어 {stats.ClearedStageCount} · 별 {stats.TotalStars}";
            var info = Label(body, $"{p.Email} · 코인 {p.Coin}{record}", 22, TextAnchor.UpperLeft, Muted);
            Place(info, new Vector2(0, 1), Vector2.zero, new Vector2(1160, 34), new Vector2(0, 1));

            // 닉네임
            Section(body, "닉네임 (2~20자)", -52);
            var nick = Input(body, "닉네임", limit: 20, size: 26);
            nick.text = p.Nickname ?? "";
            Place(nick, new Vector2(0, 1), new Vector2(0, -88), new Vector2(560, 64), new Vector2(0, 1));
            var nickStatus = Label(body, "", 20, TextAnchor.UpperLeft, Muted);
            Place(nickStatus, new Vector2(0, 1), new Vector2(0, -158), new Vector2(1160, 30), new Vector2(0, 1));
            var check = Button(body, "중복 확인", null, 22, new Color32(0x22, 0x26, 0x2e, 0xff));
            Place(check, new Vector2(0, 1), new Vector2(576, -88), new Vector2(170, 64), new Vector2(0, 1));
            var saveNick = Button(body, "변경", null, 22);
            Place(saveNick, new Vector2(0, 1), new Vector2(762, -88), new Vector2(140, 64), new Vector2(0, 1));
            check.onClick.AddListener(() => Run(check, nickStatus, () => actions.CheckNickname(nick.text.Trim())));
            saveNick.onClick.AddListener(() => Run(saveNick, nickStatus, () => actions.SaveNickname(nick.text.Trim())));

            // 자기소개
            Section(body, "자기소개 (255자 이하, 비우면 지움)", -200);
            var comment = Input(body, "자기소개", limit: 255, size: 24);
            comment.text = p.Comment ?? "";
            Place(comment, new Vector2(0, 1), new Vector2(0, -236), new Vector2(902, 64), new Vector2(0, 1));
            var saveComment = Button(body, "저장", null, 22);
            Place(saveComment, new Vector2(0, 1), new Vector2(918, -236), new Vector2(140, 64), new Vector2(0, 1));
            var commentStatus = Label(body, "", 20, TextAnchor.UpperLeft, Muted);
            Place(commentStatus, new Vector2(0, 1), new Vector2(0, -306), new Vector2(1160, 30), new Vector2(0, 1));
            saveComment.onClick.AddListener(() => Run(saveComment, commentStatus, () => actions.SaveComment(comment.text)));

            // 장착: 스킨, 테마
            EquipRow(body, "스킨", Data.ItemTypes.Skin, p.EquippedSkinId, items, -350, id => actions.Equip(id, p.EquippedThemeId));
            EquipRow(body, "테마", Data.ItemTypes.Theme, p.EquippedThemeId, items, -560, id => actions.Equip(p.EquippedSkinId, id));
        }

        private static void Section(RectTransform body, string title, float y)
        {
            var t = Label(body, title, 22, TextAnchor.UpperLeft, bold: true);
            Place(t, new Vector2(0, 1), new Vector2(0, y), new Vector2(1160, 32), new Vector2(0, 1));
        }

        /// <summary>장착 줄: "기본" + 가진 아이템 버튼 (최대 6개). 지금 장착한 것은 강조</summary>
        private static void EquipRow(RectTransform body, string label, string type, long? equipped,
            IReadOnlyList<Data.MyItem> items, float y, Action<long?> onPick)
        {
            var owned = items.Where(i => i.ItemType == type).ToList();
            string current = equipped == null ? "기본" : owned.FirstOrDefault(i => i.ItemId == equipped)?.Name ?? $"#{equipped}";
            Section(body, $"{label} · 지금: {current}", y);
            float x = 0;
            var choices = new List<(long? id, string name)> { (null, "기본") };
            choices.AddRange(owned.Take(6).Select(i => ((long?)i.ItemId, i.Name)));
            foreach (var (id, name) in choices)
            {
                bool on = id == equipped;
                long? pick = id;
                var b = Button(body, on ? "✓ " + name : name, () => { if (!on) onPick(pick); }, 22,
                    on ? (Color?)Line : new Color32(0x22, 0x26, 0x2e, 0xff));
                Place(b, new Vector2(0, 1), new Vector2(x, y - 40), new Vector2(180, 64), new Vector2(0, 1));
                x += 192;
            }
            if (owned.Count == 0)
            {
                var none = Label(body, $"가진 {label}이(가) 없습니다. 상점에서 살 수 있습니다.", 20, TextAnchor.MiddleLeft, Muted);
                Place(none, new Vector2(0, 1), new Vector2(200, y - 40), new Vector2(900, 64), new Vector2(0, 1));
            }
            else if (owned.Count > 6)
            {
                var more = Label(body, $"외 {owned.Count - 6}개", 20, TextAnchor.MiddleLeft, Muted);
                Place(more, new Vector2(0, 1), new Vector2(x, y - 40), new Vector2(200, 64), new Vector2(0, 1));
            }
        }

        /// <summary>버튼 동작을 실행하고 결과 문구를 status에 보여 준다 (실행 중에는 버튼을 막는다)</summary>
        private static async void Run(Button button, Text status, Func<Task<string>> action)
        {
            button.interactable = false;
            status.text = "처리 중...";
            status.color = Muted;
            try
            {
                string message = await action();
                if (status == null) return;
                status.text = message;
                status.color = message.StartsWith("✓") ? Good : Warn;
            }
            finally
            {
                if (button != null) button.interactable = true;
            }
        }

        /// <summary>메인 화면 종료 팝업: 시작 화면으로 / 게임 종료 / 취소</summary>
        public void ShowQuit(Action toTitle, Action quit)
        {
            Popup(_popupRoot, "종료", new Vector2(760, 420), out RectTransform body);
            var t = Label(body, "시작 화면으로 돌아가거나 게임을 종료할 수 있습니다.", 26, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(0, 90);
            var exit = Button(body, "게임 종료", () => quit(), 24);
            Place(exit, new Vector2(1, 0), Vector2.zero, new Vector2(200, 68), new Vector2(1, 0));
            var title = Button(body, "시작 화면으로", () => { ClosePopups(); toTitle(); }, 24);
            Place(title, new Vector2(1, 0), new Vector2(-216, 0), new Vector2(200, 68), new Vector2(1, 0));
            var cancel = Button(body, "취소", () => ClosePopups(), 24, new Color32(0x22, 0x26, 0x2e, 0xff));
            Place(cancel, new Vector2(1, 0), new Vector2(-432, 0), new Vector2(200, 68), new Vector2(1, 0));
        }

        /// <summary>확인 팝업 (예 / 아니오)</summary>
        public void ShowConfirm(string title, string text, Action onYes)
        {
            Popup(_popupRoot, title, new Vector2(760, 420), out RectTransform body);
            var t = Label(body, text, 26, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(0, 90);
            var yes = Button(body, "예", () => onYes(), 26);
            Place(yes, new Vector2(1, 0), Vector2.zero, new Vector2(200, 68), new Vector2(1, 0));
            var no = Button(body, "아니오", () => ClosePopups(), 24, new Color32(0x22, 0x26, 0x2e, 0xff));
            Place(no, new Vector2(1, 0), new Vector2(-216, 0), new Vector2(200, 68), new Vector2(1, 0));
        }

        /// <summary>메일 자세히 팝업. 받지 않았으면 받기 버튼</summary>
        public void ShowMail(Data.MailResponse mail, Action onClaim)
        {
            Popup(_popupRoot, mail.Title, new Vector2(1000, 640), out RectTransform body);
            string expiry = mail.ExpiresAt == null ? "무기한" : $"{mail.ExpiresAt.Value.ToLocalTime():yyyy.MM.dd HH:mm}까지";
            var meta = Label(body, $"{mail.CreatedAt.ToLocalTime():yyyy.MM.dd HH:mm} · {expiry}", 20, TextAnchor.UpperLeft, Muted);
            Place(meta, new Vector2(0, 1), Vector2.zero, new Vector2(920, 32), new Vector2(0, 1));
            var content = Label(body, string.IsNullOrEmpty(mail.Content) ? "(내용 없음)" : mail.Content, 24, TextAnchor.UpperLeft);
            content.lineSpacing = 1.3f;
            content.verticalOverflow = VerticalWrapMode.Truncate;
            Place(content, new Vector2(0, 1), new Vector2(0, -48), new Vector2(920, 300), new Vector2(0, 1));
            var reward = Label(body, "보상: " + mail.RewardText, 24, TextAnchor.MiddleLeft, Warn, bold: true);
            Place(reward, new Vector2(0, 0), new Vector2(0, 88), new Vector2(920, 40), new Vector2(0, 0));
            if (mail.Claimed)
            {
                var done = Label(body, "이미 받은 메일입니다.", 24, TextAnchor.MiddleRight, Muted);
                Place(done, new Vector2(1, 0), Vector2.zero, new Vector2(400, 72), new Vector2(1, 0));
            }
            else
            {
                var claim = Button(body, "받기", () => onClaim(), 28);
                Place(claim, new Vector2(1, 0), Vector2.zero, new Vector2(240, 72), new Vector2(1, 0));
            }
        }

        private const int GuidebookColumns = 4, GuidebookRows = 3;
        /// <summary>도감 한 쪽에 보이는 칸 수</summary>
        public const int GuidebookPageSize = GuidebookColumns * GuidebookRows;

        /// <summary>
        /// 도감 팝업. 위에 분류 탭(전체·오류·바이러스·개발 용어)과 해금 수, 가운데 카드 4x3, 아래에 쪽 넘기기.
        /// 해금한 카드는 이름과 해금 날짜, 누르면 상세. 잠긴 카드는 "???"
        /// </summary>
        public void ShowGuidebook(Data.GuidebookListResponse list, string filter, int page, Action<string> onFilter,
            Action<int> onPage, Action<long> onOpen)
        {
            Popup(_popupRoot, $"도감 · 해금 {list.UnlockedCount} / {list.TotalCount}", new Vector2(1240, 880), out RectTransform body);
            float x = 0;
            var tabs = new List<(string, string)> { (null, "전체") };
            tabs.AddRange(Data.GuidebookCategories.All.Select(c => (c, Data.GuidebookCategories.Label(c))));
            foreach (var (type, label) in tabs)
            {
                string t = type;
                var tab = Button(body, label, () => onFilter(t), 22, t == filter ? (Color?)Line : null);
                Place(tab, new Vector2(0, 1), new Vector2(x, 0), new Vector2(180, 56), new Vector2(0, 1));
                x += 190;
            }
            if (list.Items.Count == 0)
            {
                var empty = Label(body, "아직 등록된 도감 항목이 없습니다.", 28, TextAnchor.MiddleCenter, Muted);
                Stretch(empty.rectTransform);
                return;
            }

            int pages = (list.Items.Count + GuidebookPageSize - 1) / GuidebookPageSize;
            page = Mathf.Clamp(page, 0, pages - 1);
            const float cardW = 275, cardH = 170, gap = 20;
            int i = 0;
            foreach (var it in list.Items.Skip(page * GuidebookPageSize).Take(GuidebookPageSize))
            {
                var item = it;
                float cx = (i % GuidebookColumns) * (cardW + gap);
                float cy = -76 - (i / GuidebookColumns) * (cardH + gap);
                Color color = it.Unlocked ? (Color)new Color32(0x16, 0x1e, 0x2a, 0xff) : new Color32(0x22, 0x26, 0x2e, 0xff);
                var card = Button(body, "", () => { if (item.Unlocked) onOpen(item.GuidebookId); }, 24, color);
                card.interactable = it.Unlocked;
                Place(card, new Vector2(0, 1), new Vector2(cx, cy), new Vector2(cardW, cardH), new Vector2(0, 1));
                var cat = Label(card.transform, $"{Data.GuidebookCategories.Label(it.Category)} · No.{it.GuidebookId}", 18, TextAnchor.UpperLeft, Muted);
                Stretch(cat.rectTransform, 14);
                var title = Label(card.transform, it.Unlocked ? it.Title : "???", 26, TextAnchor.MiddleLeft, it.Unlocked ? TextColor : Muted, bold: true);
                Stretch(title.rectTransform, 14);
                var date = Label(card.transform, it.Unlocked && it.UnlockedAt != null ? $"해금 {it.UnlockedAt.Value.ToLocalTime():yyyy.MM.dd}" : "잠김",
                    18, TextAnchor.LowerLeft, it.Unlocked ? Good : Muted);
                Stretch(date.rectTransform, 14);
                i++;
            }

            if (pages > 1)
            {
                var prev = Button(body, "이전", () => onPage(page - 1), 22);
                prev.interactable = page > 0;
                Place(prev, new Vector2(0.5f, 0), new Vector2(-200, 0), new Vector2(160, 56), new Vector2(0.5f, 0));
                var pageLabel = Label(body, $"{page + 1} / {pages}", 24, TextAnchor.MiddleCenter, Muted);
                Place(pageLabel, new Vector2(0.5f, 0), Vector2.zero, new Vector2(200, 56), new Vector2(0.5f, 0));
                var next = Button(body, "다음", () => onPage(page + 1), 22);
                next.interactable = page < pages - 1;
                Place(next, new Vector2(0.5f, 0), new Vector2(200, 0), new Vector2(160, 56), new Vector2(0.5f, 0));
            }
        }

        /// <summary>도감 상세 팝업. 왼쪽 그림(없으면 상자), 오른쪽 분류·이름·처음 등장·해금일, 아래 설명</summary>
        /// <param name="stageLabel">처음 등장하는 스테이지 이름 (모르면 null)</param>
        public void ShowGuidebookDetail(Data.GuidebookDetail d, string stageLabel, Sprite image)
        {
            Popup(_popupRoot, d.Title, new Vector2(1240, 820), out RectTransform body);
            var frame = Panel(body, "Image", new Color32(0x16, 0x22, 0x32, 0xff), Muted);
            Place(frame, new Vector2(0, 1), Vector2.zero, new Vector2(360, 360), new Vector2(0, 1));
            if (image != null)
            {
                var img = new GameObject("Sprite", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(frame, false);
                img.sprite = image;
                img.preserveAspect = true;
                Stretch(img.rectTransform, 8);
            }
            else
            {
                var none = Label(frame, "이미지 없음", 24, TextAnchor.MiddleCenter, Muted);
                Stretch(none.rectTransform);
            }
            var info = new List<string> { $"분류: {Data.GuidebookCategories.Label(d.Category)}", $"번호: No.{d.GuidebookId}" };
            if (!string.IsNullOrEmpty(stageLabel)) info.Add($"처음 등장: {stageLabel}");
            if (d.UnlockedAt != null) info.Add($"해금: {d.UnlockedAt.Value.ToLocalTime():yyyy.MM.dd HH:mm}");
            var meta = Label(body, string.Join("\n", info), 24, TextAnchor.UpperLeft, Muted);
            meta.lineSpacing = 1.4f;
            Place(meta, new Vector2(0, 1), new Vector2(400, 0), new Vector2(760, 360), new Vector2(0, 1));
            var comment = Label(body, d.Comment ?? "", 26, TextAnchor.UpperLeft);
            comment.lineSpacing = 1.3f;
            comment.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(comment.rectTransform);
            comment.rectTransform.offsetMax = new Vector2(0, -390);
        }

        /// <summary>공지 목록 팝업. 항목을 누르면 onOpen(공지 ID)</summary>
        public void ShowNotices(IReadOnlyList<Data.NoticeSummary> notices, Action<long> onOpen)
        {
            Popup(_popupRoot, "공지사항", new Vector2(1000, 720), out RectTransform body);
            if (notices.Count == 0)
            {
                var empty = Label(body, "지금 게시 중인 공지가 없습니다.", 28, TextAnchor.MiddleCenter, Muted);
                Stretch(empty.rectTransform);
                return;
            }
            float y = 0;
            foreach (var n in notices.Take(7))
            {
                string pin = n.IsPinned ? "[고정] " : "";
                string date = n.PublishedAt.ToLocalTime().ToString("MM.dd");
                long id = n.NoticeId;
                var b = Button(body, $"{pin}[{n.CategoryLabel}] {n.Title}   {date}", () => onOpen(id), 26);
                var text = b.GetComponentInChildren<Text>();
                text.alignment = TextAnchor.MiddleLeft;
                text.rectTransform.offsetMin = new Vector2(24, 0);
                Place(b, new Vector2(0, 1), new Vector2(0, y), new Vector2(920, 72), new Vector2(0, 1));
                y -= 82;
            }
            if (notices.Count > 7)
            {
                var more = Label(body, $"외 {notices.Count - 7}개 (최근 7개만 표시)", 22, TextAnchor.UpperLeft, Muted);
                Place(more, new Vector2(0, 1), new Vector2(0, y - 6), new Vector2(920, 36), new Vector2(0, 1));
            }
        }

        /// <summary>공지 상세 팝업</summary>
        public void ShowNoticeDetail(Data.NoticeDetail notice)
        {
            Popup(_popupRoot, notice.Title, new Vector2(1100, 780), out RectTransform body);
            string updated = notice.UpdatedAt > notice.PublishedAt.AddMinutes(1)
                ? $" · 수정 {notice.UpdatedAt.ToLocalTime():yyyy.MM.dd HH:mm}" : "";
            var meta = Label(body, $"[{notice.CategoryLabel}] {notice.PublishedAt.ToLocalTime():yyyy.MM.dd HH:mm}{updated}",
                22, TextAnchor.UpperLeft, Muted);
            Place(meta, new Vector2(0, 1), Vector2.zero, new Vector2(1020, 36), new Vector2(0, 1));
            var content = Label(body, notice.Content, 26, TextAnchor.UpperLeft);
            content.lineSpacing = 1.3f;
            content.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(content.rectTransform);
            content.rectTransform.offsetMax = new Vector2(0, -52);
        }

        public void ShowList(string title, IEnumerable<string> lines)
        {
            Popup(_popupRoot, title, new Vector2(900, 620), out RectTransform body);
            var t = Label(body, string.Join("\n", lines), 28, TextAnchor.UpperLeft);
            t.lineSpacing = 1.4f;
            Stretch(t.rectTransform);
        }

        /// <summary>설정 팝업. 소리 설정은 아직 값만 저장한다</summary>
        /// <param name="bugReport">버그 제보 (null이면 버튼 없음)</param>
        /// <param name="myReports">내 신고 목록 (null이면 버튼 없음)</param>
        public void ShowSettings(string account, bool loggedIn, Action logout, Action bugReport = null, Action myReports = null)
        {
            Popup(_popupRoot, "설정", new Vector2(820, 560), out RectTransform body);
            float y = 0;
            foreach (var (label, key) in new[] { ("배경음", "executor.settings.bgm"), ("효과음", "executor.settings.sfx") })
            {
                var l = Label(body, label, 28, TextAnchor.MiddleLeft);
                Place(l, new Vector2(0, 1), new Vector2(0, y), new Vector2(160, 50), new Vector2(0, 1));
                var slider = MakeSlider(body, PlayerPrefs.GetFloat(key, 0.8f));
                Place(slider, new Vector2(0, 1), new Vector2(180, y - 15), new Vector2(520, 20), new Vector2(0, 1));
                slider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); });
                y -= 80;
            }
            var acc = Label(body, $"계정: {account}\n서버: {Network.ApiConfig.BaseUrl}", 22, TextAnchor.UpperLeft, Muted);
            Place(acc, new Vector2(0, 1), new Vector2(0, y - 10), new Vector2(740, 90), new Vector2(0, 1));
            if (loggedIn)
            {
                var b = Button(body, "로그아웃", null, 26);
                Place(b, new Vector2(0, 0), Vector2.zero, new Vector2(220, 64), new Vector2(0, 0));
                b.onClick.AddListener(() => { ClosePopups(); logout?.Invoke(); });
            }
            if (loggedIn && myReports != null)
            {
                var mine = Button(body, "내 신고", () => { ClosePopups(); myReports(); }, 24);
                Place(mine, new Vector2(1, 0), Vector2.zero, new Vector2(200, 64), new Vector2(1, 0));
            }
            if (loggedIn && bugReport != null)
            {
                var bug = Button(body, "버그 제보", () => { ClosePopups(); bugReport(); }, 24);
                Place(bug, new Vector2(1, 0), new Vector2(-216, 0), new Vector2(200, 64), new Vector2(1, 0));
            }
        }

        /// <summary>로그인 / 회원가입 팝업. 성공하면 닫고 onSuccess. 실패하면 서버 오류 메시지를 보여 준다</summary>
        public void ShowLogin(Func<string, string, Task> login, Func<string, string, Task> signup, Action onSuccess)
        {
            bool signupMode = false;
            GameObject popup = Popup(_popupRoot, "로그인", new Vector2(820, 600), out RectTransform body);
            Text header = popup.GetComponentsInChildren<Text>()[0];

            var email = Input(body, "이메일", limit: 255);
            Place(email, new Vector2(0, 1), Vector2.zero, new Vector2(740, 72), new Vector2(0, 1));
            var password = Input(body, "비밀번호 (8~64자)", password: true, limit: 64);
            Place(password, new Vector2(0, 1), new Vector2(0, -90), new Vector2(740, 72), new Vector2(0, 1));
            var confirm = Input(body, "비밀번호 재입력", password: true, limit: 64);
            Place(confirm, new Vector2(0, 1), new Vector2(0, -180), new Vector2(740, 72), new Vector2(0, 1));
            confirm.gameObject.SetActive(false);

            var error = Label(body, "", 22, TextAnchor.UpperLeft, Bad);
            Place(error, new Vector2(0, 1), new Vector2(0, -264), new Vector2(740, 84), new Vector2(0, 1));

            Button submit = Button(body, "로그인", null, 28);
            Place(submit, new Vector2(0, 0), Vector2.zero, new Vector2(358, 72), new Vector2(0, 0));
            Button toggle = Button(body, "회원가입으로", null, 24, new Color32(0x22, 0x26, 0x2e, 0xff));
            Place(toggle, new Vector2(1, 0), Vector2.zero, new Vector2(358, 72), new Vector2(1, 0));

            toggle.onClick.AddListener(() =>
            {
                signupMode = !signupMode;
                header.text = signupMode ? "회원가입" : "로그인";
                SetButtonText(submit, signupMode ? "가입하기" : "로그인");
                SetButtonText(toggle, signupMode ? "로그인으로" : "회원가입으로");
                confirm.gameObject.SetActive(signupMode);
                error.text = "";
            });
            async void Submit()
            {
                if (submit == null || !submit.interactable) return;
                error.text = "";
                submit.interactable = false;
                try
                {
                    if (signupMode)
                    {
                        if (password.text.Length < 8 || password.text.Length > 64) { error.text = "비밀번호는 8~64자여야 합니다."; return; }
                        if (password.text != confirm.text) { error.text = "비밀번호가 서로 다릅니다."; return; }
                        await signup(email.text.Trim(), password.text);
                    }
                    else await login(email.text.Trim(), password.text);
                    Object.Destroy(popup);
                    onSuccess?.Invoke();
                }
                catch (Network.ApiException e)
                {
                    error.text = e.IsNetworkError ? "서버에 연결할 수 없습니다. (네트워크·VPN 확인)" : e.Message;
                }
                finally
                {
                    if (submit != null) submit.interactable = true;
                }
            }
            submit.onClick.AddListener(Submit);

            // Enter: 로그인은 이메일 → 비밀번호 → 로그인, 회원가입은 이메일 → 비밀번호 → 비밀번호 재입력 → 가입하기.
            // 이메일·비밀번호가 비어 있으면 Enter를 눌러도 그 칸에 머문다
            OnEnter(email, () => MoveFocus(email.text.Trim().Length == 0 ? email : password));
            OnEnter(password, () =>
            {
                if (password.text.Length == 0) MoveFocus(password);
                else if (signupMode) MoveFocus(confirm);
                else Submit();
            });
            OnEnter(confirm, Submit);
            // 로그인 ↔ 회원가입을 바꾸면 이메일부터 (이미 썼으면 비밀번호부터)
            toggle.onClick.AddListener(() => MoveFocus(email.text.Trim().Length == 0 ? email : password));
            MoveFocus(email);
        }

        /// <summary>입력칸으로 포커스를 옮긴다: 바로 옮기고, 한 프레임 뒤에도 그 칸이 아니면 한 번 더 (Enter 처리와 겹칠 때 대비)</summary>
        private void MoveFocus(InputField field)
        {
            FocusField(field);
            StartCoroutine(FocusRoutine(field));
        }

        private static IEnumerator FocusRoutine(InputField field)
        {
            yield return null;
            if (field == null || !field.gameObject.activeInHierarchy || field.isFocused) yield break;
            FocusField(field);
        }

        private static Slider MakeSlider(Transform parent, float value)
        {
            var root = Panel(parent, "Slider", new Color32(0x1c, 0x26, 0x34, 0xff));
            var fill = Panel(root, "Fill", Line);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var handle = Panel(root, "Handle", TextColor);
            handle.sizeDelta = new Vector2(24, 36);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.value = value;
            return slider;
        }
    }
}
