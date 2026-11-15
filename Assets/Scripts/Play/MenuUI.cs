using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.UiKit;
using Object = UnityEngine.Object;

namespace Executor.Play
{
    /// <summary>
    /// 메뉴 화면: 시작(index) → 메인(main) → 스테이지 선택(stage). 와이어프레임 배치를 따른다.
    /// 팝업(로그인, 설정, 프로필, 진행 상황, 안내)도 여기서 띄운다.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        public class StageNode
        {
            public int No;
            public string Title;
            public bool Unlocked;
            public bool Cleared;
            public string Detail;
        }

        private RectTransform _root;
        private RectTransform _popupRoot;
        private GameObject _title, _main, _stage;
        private Text _titleAccount, _titleHint;
        private Button _titleLogin, _titleLogout;
        private Text _profileName, _profileCoin;
        private RectTransform _stageMap;

        public bool HasPopup => _popupRoot.childCount > 0;

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

        // ================= 시작 화면 =================
        public Action OnTitleStart, OnTitleLogin, OnTitleLogout, OnSettings;

        public void ShowTitle(string account, bool loggedIn)
        {
            HideAll();
            _title.SetActive(true);
            _titleAccount.text = account;
            _titleLogin.gameObject.SetActive(!loggedIn);
            _titleLogout.gameObject.SetActive(loggedIn);
        }

        private GameObject BuildTitle()
        {
            var screen = Panel(_root, "Title", Background);
            Stretch(screen);
            // 화면 아무 데나 클릭하면 시작 (다른 버튼보다 뒤에 깔린 투명 버튼)
            var touch = screen.gameObject.AddComponent<Button>();
            touch.transition = Selectable.Transition.None;
            touch.onClick.AddListener(() => OnTitleStart?.Invoke());

            var logo = Panel(screen, "Logo", new Color32(0x16, 0x22, 0x32, 0xff), Line);
            Place(logo, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(760, 300));
            var logoText = Label(logo, "EXECUTOR\n집행자", 72, TextAnchor.MiddleCenter, bold: true);
            Stretch(logoText.rectTransform);

            _titleHint = Label(screen, "화면을 클릭해 게임을 시작하세요", 30, TextAnchor.MiddleCenter, Muted);
            Place(_titleHint, new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(1000, 50));

            var settings = Button(screen, "설정", () => OnSettings?.Invoke(), 24);
            Place(settings, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(110, 110), new Vector2(1, 1));

            _titleAccount = Label(screen, "", 24, TextAnchor.MiddleLeft, Muted);
            Place(_titleAccount, new Vector2(0, 0), new Vector2(40, 48), new Vector2(800, 40), new Vector2(0, 0));
            _titleLogin = Button(screen, "로그인", () => OnTitleLogin?.Invoke(), 24);
            Place(_titleLogin, new Vector2(0, 0), new Vector2(40, 100), new Vector2(200, 60), new Vector2(0, 0));
            _titleLogout = Button(screen, "로그아웃", () => OnTitleLogout?.Invoke(), 24);
            Place(_titleLogout, new Vector2(0, 0), new Vector2(40, 100), new Vector2(200, 60), new Vector2(0, 0));
            return screen.gameObject;
        }

        private void Update()
        {
            if (_title != null && _title.activeSelf)
            {
                var c = _titleHint.color;
                c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f));
                _titleHint.color = c;
            }
        }

        // ================= 메인 화면 =================
        public Action OnProfile, OnMail, OnShop, OnStages, OnMainBack;

        public void ShowMain(string nickname, string coin)
        {
            HideAll();
            _main.SetActive(true);
            _profileName.text = nickname;
            _profileCoin.text = coin;
        }

        private GameObject BuildMain()
        {
            var screen = Panel(_root, "Main", Background);
            Stretch(screen);

            // 프로필 (왼쪽 위)
            var profile = Button(screen, "", () => OnProfile?.Invoke(), 24);
            Place(profile, new Vector2(0, 1), new Vector2(40, -40), new Vector2(420, 110), new Vector2(0, 1));
            _profileName = profile.GetComponentInChildren<Text>();
            _profileName.alignment = TextAnchor.UpperLeft;
            _profileName.rectTransform.offsetMin = new Vector2(24, 12);
            _profileName.rectTransform.offsetMax = new Vector2(-24, -14);
            _profileCoin = Label(profile.transform, "", 22, TextAnchor.LowerLeft, Muted);
            Stretch(_profileCoin.rectTransform);
            _profileCoin.rectTransform.offsetMin = new Vector2(24, 14);
            _profileCoin.rectTransform.offsetMax = new Vector2(-24, -12);

            // 우편, 상점 (왼쪽)
            var mail = Button(screen, "우편", () => OnMail?.Invoke(), 24);
            Place(mail, new Vector2(0, 1), new Vector2(40, -190), new Vector2(110, 90), new Vector2(0, 1));
            var shop = Button(screen, "상점", () => OnShop?.Invoke(), 24);
            Place(shop, new Vector2(0, 1), new Vector2(40, -300), new Vector2(110, 90), new Vector2(0, 1));

            // 시나리오 일러스트 (가운데)
            var art = Panel(screen, "ScenarioArt", new Color32(0x16, 0x22, 0x32, 0xff), Muted);
            Place(art, new Vector2(0.5f, 0.5f), new Vector2(-40, 40), new Vector2(1000, 560));
            var artText = Label(art, "시나리오에 따른 화면 일러스트\n(이미지 자리)", 30, TextAnchor.MiddleCenter, Muted);
            Stretch(artText.rectTransform);

            // 설정 (오른쪽 위), 스테이지 (오른쪽 아래), 시작 화면으로 (왼쪽 아래)
            var settings = Button(screen, "설정", () => OnSettings?.Invoke(), 24);
            Place(settings, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(110, 110), new Vector2(1, 1));
            var stages = Button(screen, "스테이지", () => OnStages?.Invoke(), 34);
            Place(stages, new Vector2(1, 0), new Vector2(-60, 50), new Vector2(300, 300), new Vector2(1, 0));
            var back = Button(screen, "시작 화면", () => OnMainBack?.Invoke(), 22);
            Place(back, new Vector2(0, 0), new Vector2(40, 40), new Vector2(180, 56), new Vector2(0, 0));
            return screen.gameObject;
        }

        // ================= 스테이지 선택 =================
        public Action OnStageBack, OnProgress, OnScenario;
        public Action<int> OnStageSelected;

        public void ShowStageSelect(string chapter, IReadOnlyList<StageNode> nodes)
        {
            HideAll();
            _stage.SetActive(true);
            Clear(_stageMap);

            var title = Label(_stageMap, chapter, 34, TextAnchor.MiddleCenter, bold: true);
            Place(title, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(1000, 60));

            // 지그재그 배치 (와이어프레임 stage 화면)
            float[] heights = { 380, 520, 420, 560, 400, 540, 440, 580 };
            var centers = new List<Vector2>();
            for (int i = 0; i < nodes.Count; i++)
            {
                float x = nodes.Count == 1 ? 960 : Mathf.Lerp(260, 1660, i / (float)(nodes.Count - 1));
                centers.Add(new Vector2(x, heights[i % heights.Length]));
            }
            for (int i = 0; i + 1 < centers.Count; i++) DashedLine(_stageMap, centers[i], centers[i + 1], nodes[i + 1].Unlocked);
            for (int i = 0; i < nodes.Count; i++)
            {
                StageNode node = nodes[i];
                Color color = node.Cleared ? new Color32(0x1f, 0x4d, 0x3a, 0xff)
                            : node.Unlocked ? ButtonColor : new Color32(0x22, 0x26, 0x2e, 0xff);
                var b = Button(_stageMap, node.Unlocked ? node.No.ToString() : "잠김", () => OnStageSelected?.Invoke(node.No),
                    node.Unlocked ? 40 : 22, color);
                b.interactable = node.Unlocked;
                Place(b, new Vector2(0, 0), centers[i], new Vector2(110, 110), new Vector2(0.5f, 0.5f));
                var caption = Label(_stageMap, node.Title + (node.Detail != null ? "\n" + node.Detail : ""), 20,
                    TextAnchor.UpperCenter, node.Unlocked ? TextColor : Muted);
                Place(caption, new Vector2(0, 0), centers[i] + new Vector2(0, -70), new Vector2(240, 80), new Vector2(0.5f, 1));
            }
        }

        private GameObject BuildStage()
        {
            var screen = Panel(_root, "StageSelect", Background);
            Stretch(screen);
            _stageMap = new GameObject("Map", typeof(RectTransform)).GetComponent<RectTransform>();
            _stageMap.SetParent(screen, false);
            Stretch(_stageMap);
            var back = Button(screen, "돌아가기", () => OnStageBack?.Invoke(), 24);
            Place(back, new Vector2(0, 1), new Vector2(40, -40), new Vector2(160, 70), new Vector2(0, 1));
            var settings = Button(screen, "설정", () => OnSettings?.Invoke(), 24);
            Place(settings, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(110, 110), new Vector2(1, 1));
            var progress = Button(screen, "진행 상황", () => OnProgress?.Invoke(), 26);
            Place(progress, new Vector2(1, 0), new Vector2(-60, 150), new Vector2(260, 80), new Vector2(1, 0));
            var scenario = Button(screen, "시나리오", () => OnScenario?.Invoke(), 26);
            Place(scenario, new Vector2(1, 0), new Vector2(-60, 50), new Vector2(260, 80), new Vector2(1, 0));
            return screen.gameObject;
        }

        private static void DashedLine(Transform parent, Vector2 a, Vector2 b, bool active)
        {
            Vector2 dir = (b - a).normalized;
            float length = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Color color = active ? Line : Muted;
            for (float d = 70; d < length - 70; d += 22)
            {
                var dash = Panel(parent, "Dash", color);
                Place(dash, new Vector2(0, 0), a + dir * d, new Vector2(12, 3), new Vector2(0.5f, 0.5f));
                dash.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }

        // ================= 팝업 =================
        /// <summary>단순 안내 팝업</summary>
        public void ShowMessage(string title, string text)
        {
            Popup(_popupRoot, title, new Vector2(760, 360), out RectTransform body);
            var t = Label(body, text, 28, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
        }

        /// <summary>목록 팝업 (진행 상황, 프로필 등)</summary>
        public void ShowList(string title, IEnumerable<string> lines)
        {
            Popup(_popupRoot, title, new Vector2(900, 620), out RectTransform body);
            var t = Label(body, string.Join("\n", lines), 28, TextAnchor.UpperLeft);
            t.lineSpacing = 1.4f;
            Stretch(t.rectTransform);
        }

        /// <summary>설정 팝업. 소리 설정은 아직 값만 저장한다</summary>
        public void ShowSettings(string account, bool loggedIn, Action logout)
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
        }

        /// <summary>
        /// 로그인 / 회원가입 팝업. 성공하면 닫고 onSuccess. 실패하면 서버 오류 메시지를 보여 준다.
        /// </summary>
        public void ShowLogin(Func<string, string, Task> login, Func<string, string, string, Task> signup,
                              Action onSuccess, Action onOffline)
        {
            bool signupMode = false;
            GameObject popup = Popup(_popupRoot, "로그인", new Vector2(820, 640), out RectTransform body);
            Text header = popup.GetComponentsInChildren<Text>()[0];

            var email = Input(body, "이메일", limit: 255);
            Place(email, new Vector2(0, 1), Vector2.zero, new Vector2(740, 72), new Vector2(0, 1));
            var password = Input(body, "비밀번호 (8~64자)", password: true, limit: 64);
            Place(password, new Vector2(0, 1), new Vector2(0, -90), new Vector2(740, 72), new Vector2(0, 1));
            var nickname = Input(body, "닉네임 (2~20자)", limit: 20);
            Place(nickname, new Vector2(0, 1), new Vector2(0, -180), new Vector2(740, 72), new Vector2(0, 1));
            nickname.gameObject.SetActive(false);

            var error = Label(body, "", 24, TextAnchor.MiddleLeft, Bad);
            Place(error, new Vector2(0, 1), new Vector2(0, -270), new Vector2(740, 40), new Vector2(0, 1));

            Button submit = Button(body, "로그인", null, 28);
            Place(submit, new Vector2(0, 0), new Vector2(0, 70), new Vector2(360, 72), new Vector2(0, 0));
            Button toggle = Button(body, "회원가입으로", null, 24);
            Place(toggle, new Vector2(1, 0), new Vector2(0, 70), new Vector2(360, 72), new Vector2(1, 0));
            Button offline = Button(body, "오프라인 테스트 (서버 없이 플레이)", null, 22, new Color32(0x22, 0x26, 0x2e, 0xff));
            Place(offline, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(740, 52), new Vector2(0.5f, 0));

            toggle.onClick.AddListener(() =>
            {
                signupMode = !signupMode;
                header.text = signupMode ? "회원가입" : "로그인";
                SetButtonText(submit, signupMode ? "가입하고 시작" : "로그인");
                SetButtonText(toggle, signupMode ? "로그인으로" : "회원가입으로");
                nickname.gameObject.SetActive(signupMode);
                error.text = "";
            });
            offline.onClick.AddListener(() => { Object.Destroy(popup); onOffline?.Invoke(); });
            submit.onClick.AddListener(async () =>
            {
                error.text = "";
                submit.interactable = false;
                try
                {
                    if (signupMode) await signup(email.text.Trim(), password.text, nickname.text.Trim());
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
            });
            email.Select();
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
