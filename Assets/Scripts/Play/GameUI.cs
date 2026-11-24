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
            _ranking = null;
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


        private void Update()
        {
            TickDialogue();
            if (_toast.activeSelf && Time.unscaledTime > _toastUntil) NextToast();
        }


    }
}
