using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.UiKit;

namespace Executor.Play
{
    /// <summary>
    /// 게임 화면 UI (기준 1920x1080). HUD는 GameUI.Hud.cs (디자인 시안 Game),
    /// 대화창은 GameUI.Dialogue.cs, 해킹은 GameUI.Hack.cs, 워프는 GameUI.Warp.cs, 나레이션은 GameUI.Story.cs. 여기에는 알림·팝업 공용 부분이 있다.
    /// 가운데 게임 화면은 BoardView가 GameArea 안에 맞춰 그린다
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        /// <summary>
        /// 게임 화면 영역 (화면 비율 0~1, 왼쪽 아래 기준). 시안(Game)의 가운데 열: 왼쪽 열(미니맵·퀘스트)과 오른쪽 열(턴·체크포인트·수집) 사이,
        /// 위 제목 아래 ~ 아래 키 안내 위. 기준 1920x1080에서 x 456~1464, 위에서 120 ~ 아래에서 108
        /// </summary>
        public static readonly Rect GameArea = new Rect(456f / 1920, 108f / 1080, 1008f / 1920, 852f / 1080);

        private RectTransform _root;
        private RectTransform _popupRoot;

        // 안내
        private GameObject _toast;
        private Text _toastText;
        private float _toastUntil;



        public bool HasPopup => _popupRoot.childCount > 0;

        /// <summary>팝업을 붙이는 자리 (친구·메시지 팝업이 쓴다)</summary>
        public Transform PopupRoot => _popupRoot;

        public void Build()
        {
            _root = Canvas(transform, "GameCanvas", 10);
            BuildHud();
            BuildToast();
            BuildDialogue();
            BuildNarration();
            BuildResult();
            BuildHack();
            _popupRoot = new GameObject("Popups", typeof(RectTransform)).GetComponent<RectTransform>();
            _popupRoot.SetParent(_root, false);
            Stretch(_popupRoot);
            HideAll();
        }

        public void HideAll()
        {
            _hud.SetActive(false);
            _toast.SetActive(false);
            _toastQueue.Clear();
            _dialogue.SetActive(false);
            _narration.SetActive(false);
            _result.SetActive(false);
            _hack.SetActive(false);
            ClosePopups();
        }

        public void ClosePopups()
        {
            _warpCards = null;
            _logOpen = false;
            Clear(_popupRoot);
        }

        /// <summary>전체 맵 팝업</summary>
        public void ShowMapPopup(string title, Texture2D texture, string legend)
        {
            Popup(_popupRoot, title, new Vector2(1600, 940), out RectTransform body);
            var img = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(body, false);
            img.texture = texture;
            img.raycastTarget = false;
            float scale = Mathf.Min(1500f / texture.width, 720f / texture.height);
            Place(img, new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(texture.width * scale, texture.height * scale), new Vector2(0.5f, 1));
            var note = Label(body, legend, 20, TextAnchor.LowerCenter, Muted);
            Stretch(note.rectTransform);
        }

        /// <summary>긴 글 팝업 (퀘스트 자세히 등)</summary>
        public void ShowDetail(string title, string text)
        {
            Popup(_popupRoot, title, new Vector2(1000, 760), out RectTransform body);
            var t = Label(body, text, 24, TextAnchor.UpperLeft);
            t.lineSpacing = 1.3f;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(t.rectTransform);
        }

        /// <summary>게임 화면 위 팝업 메뉴 (일시 정지 등). 버튼을 누르면 닫히고 동작한다</summary>
        public void ShowMenu(string title, IEnumerable<(string label, Action onClick)> buttons)
        {
            var list = buttons.ToList();
            Popup(_popupRoot, title, new Vector2(620, Mathf.Max(520, 200 + list.Count * 92)), out RectTransform body);
            float y = 0;
            foreach (var (label, onClick) in list)
            {
                var b = Button(body, label, () => { ClosePopups(); onClick?.Invoke(); }, 28);
                Place(b, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(480, 72));
                y -= 92;
            }
        }

        public void ShowMessage(string title, string text)
        {
            Popup(_popupRoot, title, new Vector2(760, 360), out RectTransform body);
            var t = Label(body, text, 28, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
        }

        // ---------------- 짧은 알림 ----------------

        // ---------------- 순위 팝업 ----------------
        /// <summary>
        /// 순위 팝업. 위에 전체·친구 탭, 아래에 순위 줄 (최대 10개, 내 줄은 강조). 내가 10위 밖이면 맨 아래에 내 순위
        /// </summary>
        public void ShowRanking(string title, Data.RankingResponse res, bool friends, long myUserId, Action<bool> onScope)
        {
            ClosePopups();
            Popup(_popupRoot, title, new Vector2(900, 900), out RectTransform body);
            float x = 0;
            foreach (var (f, label) in new[] { (false, "전체"), (true, "친구") })
            {
                bool scope = f;
                var tab = Button(body, label, () => onScope(scope), 22, scope == friends ? (Color?)Line : null);
                Place(tab, new Vector2(0, 1), new Vector2(x, 0), new Vector2(160, 56), new Vector2(0, 1));
                x += 170;
            }
            if (res.Rankings.Count == 0)
            {
                var empty = Label(body, friends ? "나와 친구 중 클리어한 사람이 없습니다." : "아직 클리어한 사람이 없습니다.", 26,
                    TextAnchor.MiddleCenter, Muted);
                Stretch(empty.rectTransform);
                return;
            }
            float y = -76;
            bool meShown = false;
            foreach (var r in res.Rankings.Take(10))
            {
                bool me = r.User?.UserId == myUserId;
                meShown |= me;
                var row = Panel(body, "Rank", me ? (Color)new Color32(0x1c, 0x3a, 0x52, 0xff) : new Color32(0x16, 0x1e, 0x2a, 0xff), me ? Line : (Color?)null);
                Place(row, new Vector2(0, 1), new Vector2(0, y), new Vector2(820, 56), new Vector2(0, 1));
                var rank = Label(row, $"{r.Rank}위", 24, TextAnchor.MiddleLeft, r.Rank <= 3 ? Warn : TextColor, bold: true);
                Stretch(rank.rectTransform, 16);
                var name = Label(row, (r.User?.Nickname ?? "(닉네임 없음)") + (me ? "  (나)" : ""), 24, TextAnchor.MiddleLeft);
                Stretch(name.rectTransform, 16);
                name.rectTransform.offsetMin = new Vector2(120, 0);
                var moves = Label(row, $"{r.BestMoves}턴", 24, TextAnchor.MiddleRight, Muted);
                Stretch(moves.rectTransform, 16);
                y -= 62;
            }
            string mine = res.Me == null ? "아직 이 스테이지를 클리어하지 않았습니다."
                        : meShown ? $"내 기록 {res.Me.BestMoves}턴 · {res.Me.Rank}위"
                        : $"내 순위 {res.Me.Rank}위 · {res.Me.BestMoves}턴";
            var foot = Label(body, mine + "\n같은 턴이면 먼저 클리어한 사람이 앞입니다.", 22, TextAnchor.LowerLeft, Muted);
            Stretch(foot.rectTransform);
        }


        private void Update()
        {
            TickDialogue();
            if (_toast.activeSelf && Time.unscaledTime > _toastUntil) NextToast();
        }


    }
}
