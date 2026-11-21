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

        // ================= 팝업 =================
        /// <summary>프로필 화면에서 쓰는 동작. 문자열 결과는 그 줄 아래에 보여 줄 안내 (성공·실패 메시지)</summary>
        public class ProfileActions
        {
            public Func<string, Task<string>> CheckNickname;
            public Func<string, Task<string>> SaveNickname;
            public Func<string, Task<string>> SaveComment;
            /// <summary>(스킨, 테마, 커서, 델타 꾸미기) 아이템 ID로 장착. null이면 기본</summary>
            public Action<long?, long?, long?, long?> Equip;
        }

        /// <summary>
        /// 프로필 팝업: 정보·기록, 닉네임(중복 확인·변경), 자기소개, 스킨·테마 장착(가진 것 중에서, 기본으로 되돌리기).
        /// </summary>
        public void ShowProfileEditor(Data.MyProfileResponse p, Data.ProfileDetailResponse stats,
            IReadOnlyList<Data.MyItem> items, ProfileActions actions)
        {
            Popup(_popupRoot, "프로필", new Vector2(1240, 1040), out RectTransform body);
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

            // 장착: 스킨, 테마, 커서, 델타 꾸미기 (PUT이라 네 값을 모두 보낸다)
            long? skin = p.EquippedSkinId, theme = p.EquippedThemeId, cursor = p.EquippedCursorId, delta = p.EquippedDeltaId;
            EquipRow(body, "스킨", Data.ItemTypes.Skin, skin, items, -340, id => actions.Equip(id, theme, cursor, delta));
            EquipRow(body, "테마", Data.ItemTypes.Theme, theme, items, -480, id => actions.Equip(skin, id, cursor, delta));
            EquipRow(body, "커서", Data.ItemTypes.Cursor, cursor, items, -620, id => actions.Equip(skin, theme, id, delta));
            EquipRow(body, "델타 꾸미기", Data.ItemTypes.Delta, delta, items, -760, id => actions.Equip(skin, theme, cursor, id));
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
                var b = Button(body, on ? "● " + name : name, () => { if (!on) onPick(pick); }, 22,
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


        public void ShowList(string title, IEnumerable<string> lines)
        {
            Popup(_popupRoot, title, new Vector2(900, 620), out RectTransform body);
            var t = Label(body, string.Join("\n", lines), 28, TextAnchor.UpperLeft);
            t.lineSpacing = 1.4f;
            Stretch(t.rectTransform);
        }

    }
}
