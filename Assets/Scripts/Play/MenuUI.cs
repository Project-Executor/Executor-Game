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
    public class MenuUI : MonoBehaviour
    {
        private const float Margin = 48;
        private const float Gap = 24;
        private static readonly Vector2 SettingsSize = new Vector2(120, 120);

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
        private Button _titleLogin, _titleLogout, _titleOffline, _titleTouch;
        private Text _profileName, _profileCoin;
        private Button _mailButton;
        private RectTransform _stageMap;
        private Text _stageTitle;
        private ZoomPan _zoom;
        private string _shownChapter;

        // 챕터 내비게이터 (오른쪽에서 밀려 나옴)
        private const float NavWidth = 620;
        private GameObject _navDim;
        private RectTransform _nav, _navList, _navViewport;
        private ScrollRect _navScroll;
        private bool _navOpen;

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
            if (_nav != null)
            {
                float target = _navOpen ? 0 : NavWidth + 20;
                var p = _nav.anchoredPosition;
                p.x = Mathf.Lerp(p.x, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
                if (Mathf.Abs(p.x - target) < 0.5f) p.x = target;
                _nav.anchoredPosition = p;
            }
            if (_title != null && _title.activeSelf && _titleHint.gameObject.activeSelf)
            {
                var c = _titleHint.color;
                c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f));
                _titleHint.color = c;
            }
        }

        // ================= 메인 화면 =================
        public Action OnProfile, OnMail, OnShop, OnNotice, OnGuidebook, OnFriends, OnStages, OnMainBack;
        private Button _friendButton;

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

            // 위쪽 줄: 프로필(왼쪽), 설정(오른쪽). 높이 120으로 맞춤
            var profile = Button(screen, "", () => OnProfile?.Invoke(), 28);
            Place(profile, new Vector2(0, 1), new Vector2(Margin, -Margin), new Vector2(440, 120), new Vector2(0, 1));
            _profileName = profile.GetComponentInChildren<Text>();
            _profileName.alignment = TextAnchor.UpperLeft;
            _profileName.rectTransform.offsetMin = new Vector2(28, 16);
            _profileName.rectTransform.offsetMax = new Vector2(-28, -20);
            _profileCoin = Label(profile.transform, "", 24, TextAnchor.LowerLeft, Muted);
            Stretch(_profileCoin.rectTransform);
            _profileCoin.rectTransform.offsetMin = new Vector2(28, 18);
            _profileCoin.rectTransform.offsetMax = new Vector2(-28, -16);
            SettingsButton(screen, () => OnSettings?.Invoke());
            // 프로필 옆: 친구·메시지 (안 읽은 메시지 + 받은 요청 수를 표시)
            _friendButton = Button(screen, "친구", () => OnFriends?.Invoke(), 26);
            Place(_friendButton, new Vector2(0, 1), new Vector2(Margin + 440 + Gap, -Margin), new Vector2(200, 120), new Vector2(0, 1));

            // 왼쪽 줄: 우편, 상점, 공지, 도감 (설정 버튼과 같은 120x120)
            float y = -(Margin + 120 + Gap);
            foreach (var (label, action) in new (string, Action)[]
                     {
                         ("우편", () => OnMail?.Invoke()), ("상점", () => OnShop?.Invoke()), ("공지", () => OnNotice?.Invoke()),
                         ("도감", () => OnGuidebook?.Invoke())
                     })
            {
                var b = Button(screen, label, action, 26);
                Place(b, new Vector2(0, 1), new Vector2(Margin, y), SettingsSize, new Vector2(0, 1));
                if (label == "우편") _mailButton = b;
                y -= 120 + Gap;
            }

            // 가운데: 시나리오 일러스트 (화면 정가운데)
            var art = Panel(screen, "ScenarioArt", new Color32(0x16, 0x22, 0x32, 0xff), Muted);
            Place(art, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1120, 600));
            var artText = Label(art, "시나리오에 따른 화면 일러스트\n(이미지 자리)", 30, TextAnchor.MiddleCenter, Muted);
            Stretch(artText.rectTransform);
            Sprite mainArt = ArtCatalog.Illustration("main");
            if (mainArt != null)
            {
                artText.gameObject.SetActive(false);
                Cover(art, mainArt);
            }

            // 아래쪽 줄: 시작 화면(왼쪽), 스테이지(오른쪽)
            var back = Button(screen, "시작 화면", () => OnMainBack?.Invoke(), 24);
            Place(back, new Vector2(0, 0), new Vector2(Margin, Margin), new Vector2(220, 64), new Vector2(0, 0));
            var stages = Button(screen, "스테이지", () => OnStages?.Invoke(), 40);
            Place(stages, new Vector2(1, 0), new Vector2(-Margin, Margin), new Vector2(320, 180), new Vector2(1, 0));
            return screen.gameObject;
        }

        // ================= 스테이지 선택 =================
        public Action OnStageBack, OnProgress, OnScenario;
        public Action<int> OnStageSelected;

        /// <summary>챕터 내비게이터의 한 줄</summary>
        public class ChapterEntry
        {
            public string Id;
            public string Title;
            public string Subtitle;
            /// <summary>소속 레이어. 같은 값끼리 한 폴더로 묶는다 (목록 순서대로)</summary>
            public string LayerId;
            public string LayerLabel;
            public string LayerDescription;
            public string Status;
            public bool Selectable;
            public bool HasStory;
            public bool Current;
            /// <summary>플레이어가 가장 멀리 연 스테이지가 있는 Operation (열린 것 중 마지막). 시나리오 창을 열면 이 줄이 보이게 한다</summary>
            public bool Frontier;
        }

        public Action<string> OnChapterSelected, OnChapterStory;

        public bool IsNavigatorOpen => _navOpen;

        /// <param name="chapterTitle">챕터 제목 (위쪽 가운데)</param>
        /// <param name="nodes">비어 있으면 "준비 중" 안내</param>
        public void ShowStageSelect(string chapterId, string chapterTitle, IReadOnlyList<StageNode> nodes)
        {
            HideAll();
            _stage.SetActive(true);
            CloseNavigator(true);
            Clear(_stageMap);
            _stageTitle.text = chapterTitle;
            if (_shownChapter != chapterId) _zoom.ResetView();
            _shownChapter = chapterId;

            if (nodes.Count == 0)
            {
                var empty = Label(_stageMap, "준비 중인 Operation입니다.\n시나리오에서 다른 Operation을 고를 수 있습니다.", 30, TextAnchor.MiddleCenter, Muted);
                Stretch(empty.rectTransform);
                return;
            }

            // 지그재그 배치 (와이어프레임 stage 화면). 화면 가운데 띠 안에서 좌우 대칭
            float[] heights = { 400, 560, 450, 610, 420, 580, 470, 630 };
            var centers = new List<Vector2>();
            for (int i = 0; i < nodes.Count; i++)
            {
                float x = nodes.Count == 1 ? 960 : Mathf.Lerp(300, 1620, i / (float)(nodes.Count - 1));
                centers.Add(new Vector2(x, heights[i % heights.Length]));
            }
            for (int i = 0; i + 1 < centers.Count; i++) DashedLine(_stageMap, centers[i], centers[i + 1], nodes[i + 1].Unlocked);
            for (int i = 0; i < nodes.Count; i++)
            {
                StageNode node = nodes[i];
                Color color = node.Cleared ? new Color32(0x1f, 0x4d, 0x3a, 0xff)
                            : node.Unlocked ? ButtonColor : new Color32(0x22, 0x26, 0x2e, 0xff);
                var b = Button(_stageMap, node.Unlocked ? node.No.ToString() : "잠김", () => OnStageSelected?.Invoke(node.No),
                    node.Unlocked ? 44 : 24, color);
                b.interactable = node.Unlocked;
                Place(b, new Vector2(0, 0), centers[i], new Vector2(120, 120), new Vector2(0.5f, 0.5f));
                var caption = Label(_stageMap, node.Title + (node.Detail != null ? "\n" + node.Detail : ""), 22,
                    TextAnchor.UpperCenter, node.Unlocked ? TextColor : Muted);
                Place(caption, new Vector2(0, 0), centers[i] + new Vector2(0, -76), new Vector2(260, 80), new Vector2(0.5f, 1));
            }
        }

        /// <summary>스테이지 지도 확대·축소 (키보드·버튼용). factor 1이면 원래 크기로</summary>
        public void ZoomStageMap(float factor)
        {
            if (Mathf.Approximately(factor, 1f)) _zoom.ResetView(); else _zoom.ZoomBy(factor);
        }

        private GameObject BuildStage()
        {
            var screen = Panel(_root, "StageSelect", Background);
            Stretch(screen);

            // 확대 가능한 지도: 뷰포트(밖은 잘라 냄) 안의 Content를 키운다. 지도 좌표는 화면 전체 기준(1920x1080)
            var viewport = Panel(screen, "Viewport", new Color(0, 0, 0, 0));
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _stageMap = new GameObject("Map", typeof(RectTransform)).GetComponent<RectTransform>();
            _stageMap.SetParent(viewport, false);
            Stretch(_stageMap);
            _zoom = viewport.gameObject.AddComponent<ZoomPan>();
            _zoom.Content = _stageMap;

            _stageTitle = Label(screen, "", 36, TextAnchor.MiddleCenter, bold: true);
            Place(_stageTitle, new Vector2(0.5f, 1), new Vector2(0, -Margin - 30), new Vector2(1000, 60));

            var back = Button(screen, "돌아가기", () => OnStageBack?.Invoke(), 26);
            Place(back, new Vector2(0, 1), new Vector2(Margin, -Margin), new Vector2(220, 72), new Vector2(0, 1));
            SettingsButton(screen, () => OnSettings?.Invoke());
            var scenario = Button(screen, "시나리오", () => OnScenario?.Invoke(), 28);
            Place(scenario, new Vector2(1, 0), new Vector2(-Margin, Margin), new Vector2(280, 80), new Vector2(1, 0));
            var progress = Button(screen, "진행 상황", () => OnProgress?.Invoke(), 28);
            Place(progress, new Vector2(1, 0), new Vector2(-Margin, Margin + 80 + Gap), new Vector2(280, 80), new Vector2(1, 0));

            // 확대 버튼 (왼쪽 아래)
            float x = Margin;
            foreach (var (label, factor) in new[] { ("+", 1.25f), ("−", 0.8f), ("맞춤", 1f) })
            {
                float f = factor;
                var z = Button(screen, label, () => ZoomStageMap(f), label == "맞춤" ? 22 : 34);
                float w = label == "맞춤" ? 100 : 72;
                Place(z, new Vector2(0, 0), new Vector2(x, Margin), new Vector2(w, 72), new Vector2(0, 0));
                x += w + 12;
            }
            var zoomHelp = Label(screen, "휠·+/− 확대, 드래그 이동, 0 맞춤", 20, TextAnchor.MiddleLeft, Muted);
            Place(zoomHelp, new Vector2(0, 0), new Vector2(Margin, Margin + 72 + 8), new Vector2(500, 30), new Vector2(0, 0));

            BuildNavigator(screen);
            return screen.gameObject;
        }

        // ---------------- 시나리오 내비게이터 (레이어 폴더 > Operation 파일 트리) ----------------
        /// <summary>접어 둔 레이어 (이번 실행 동안 유지)</summary>
        private readonly HashSet<string> _collapsedLayers = new HashSet<string>();
        private IReadOnlyList<ChapterEntry> _navEntries;

        private static readonly Color FolderColor = new Color32(0xe8, 0xb3, 0x3c, 0xff);
        private static readonly Color TreeLine = new Color32(0x3a, 0x48, 0x5a, 0xff);
        private void BuildNavigator(Transform screen)
        {
            var dim = Panel(screen, "NavDim", new Color(0, 0, 0, 0.45f));
            Stretch(dim);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => CloseNavigator());
            _navDim = dim.gameObject;

            _nav = Panel(screen, "Navigator", PanelColor, Line);
            _nav.anchorMin = new Vector2(1, 0);
            _nav.anchorMax = new Vector2(1, 1);
            _nav.pivot = new Vector2(1, 0.5f);
            _nav.sizeDelta = new Vector2(NavWidth, 0);
            _nav.anchoredPosition = new Vector2(NavWidth + 20, 0);
            var title = Label(_nav, "Operation", 36, TextAnchor.MiddleLeft, bold: true);
            Place(title, new Vector2(0, 1), new Vector2(40, -Margin), new Vector2(300, 60), new Vector2(0, 1));
            var close = Button(_nav, "닫기", () => CloseNavigator(), 24);
            Place(close, new Vector2(1, 1), new Vector2(-32, -Margin), new Vector2(120, 60), new Vector2(1, 1));
            // 목록은 스크롤된다 (마우스 휠·끌기). Viewport 밖은 잘라 낸다
            _navViewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            _navViewport.SetParent(_nav, false);
            Stretch(_navViewport);
            _navViewport.offsetMin = new Vector2(40, 40);
            _navViewport.offsetMax = new Vector2(-40, -(Margin + 90));
            _navViewport.GetComponent<Image>().color = new Color(0, 0, 0, 0); // 빈 곳에서도 휠·끌기를 받게
            _navList = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
            _navList.SetParent(_navViewport, false);
            _navList.anchorMin = new Vector2(0, 1);
            _navList.anchorMax = new Vector2(1, 1);
            _navList.pivot = new Vector2(0.5f, 1);
            _navList.sizeDelta = Vector2.zero;
            _navList.anchoredPosition = Vector2.zero;
            _navScroll = _nav.gameObject.AddComponent<ScrollRect>();
            _navScroll.content = _navList;
            _navScroll.viewport = _navViewport;
            _navScroll.horizontal = false;
            _navScroll.movementType = ScrollRect.MovementType.Clamped;
            _navScroll.scrollSensitivity = 40;
            _navDim.SetActive(false);
        }

        /// <summary>
        /// 오른쪽에서 시나리오 내비게이터를 밀어 넣는다. 파일 탐색기처럼 레이어(폴더) 아래 Operation(파일)이 들어간 트리.
        /// 레이어 줄을 누르면 접고 편다. 열 때마다 가장 멀리 진행한 Operation(Frontier)이 든 레이어만 펼치고
        /// 그 줄이 보이게 스크롤한다
        /// </summary>
        public void ShowNavigator(IReadOnlyList<ChapterEntry> chapters)
        {
            _navEntries = chapters;
            ChapterEntry frontier = chapters.LastOrDefault(c => c.Frontier) ?? chapters.FirstOrDefault(c => c.Current);
            if (frontier != null)
            {
                _collapsedLayers.Clear();
                foreach (ChapterEntry c in chapters)
                {
                    string layer = c.LayerId ?? "";
                    if (layer != (frontier.LayerId ?? "")) _collapsedLayers.Add(layer);
                }
            }
            RenderNavigator();
            ScrollNavigatorTo(_frontierRowTop);
            _navDim.SetActive(true);
            _navOpen = true;
        }

        private const float LayerRowHeight = 72, OpRowHeight = 112, TreeIndent = 56;

        /// <summary>마지막 RenderNavigator에서 Frontier 줄의 위쪽 y (목록 맨 위 = 0, 아래로 음수). 없으면 0</summary>
        private float _frontierRowTop;

        /// <summary>목록 안의 y(위쪽, 음수)가 보이게 스크롤한다. 위에 레이어 줄이 조금 보이도록 여유를 둔다</summary>
        private void ScrollNavigatorTo(float rowTop)
        {
            float max = Mathf.Max(0, _navList.sizeDelta.y - _navViewport.rect.height);
            _navScroll.StopMovement();
            _navList.anchoredPosition = new Vector2(0, Mathf.Clamp(-rowTop - (LayerRowHeight + 40), 0, max));
        }

        private void RenderNavigator()
        {
            Clear(_navList);
            float width = NavWidth - 80;
            float y = 0;
            _frontierRowTop = 0;
            // 목록 순서를 지키며 레이어별로 묶는다
            var groups = new List<(string id, List<ChapterEntry> entries)>();
            foreach (ChapterEntry c in _navEntries)
            {
                string key = c.LayerId ?? "";
                if (groups.Count == 0 || groups[groups.Count - 1].id != key) groups.Add((key, new List<ChapterEntry>()));
                groups[groups.Count - 1].entries.Add(c);
            }

            foreach (var (layerId, entries) in groups)
            {
                bool collapsed = _collapsedLayers.Contains(layerId);
                ChapterEntry first = entries[0];
                int open = entries.Count(e => e.Selectable);

                // 레이어 줄 (폴더)
                string key = layerId;
                var folder = Button(_navList, "", () =>
                {
                    if (!_collapsedLayers.Remove(key)) _collapsedLayers.Add(key);
                    RenderNavigator();
                }, 24, new Color32(0x14, 0x1c, 0x28, 0xff));
                Place(folder, new Vector2(0, 1), new Vector2(0, y), new Vector2(width, LayerRowHeight), new Vector2(0, 1));
                var toggle = Label(folder.transform, collapsed ? "+" : "−", 30, TextAnchor.MiddleCenter, Muted, bold: true);
                Place(toggle, new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(32, 40), new Vector2(0, 0.5f));
                FolderIcon(folder.transform, new Vector2(46, 0));
                var name = Label(folder.transform, string.IsNullOrEmpty(first.LayerLabel) ? "Layer" : first.LayerLabel, 26,
                    TextAnchor.MiddleLeft, bold: true);
                Stretch(name.rectTransform);
                name.rectTransform.offsetMin = new Vector2(96, 0);
                name.rectTransform.offsetMax = new Vector2(-150, 0);
                var count = Label(folder.transform, $"Operation {entries.Count}" + (open < entries.Count ? $" · 열림 {open}" : ""),
                    18, TextAnchor.MiddleRight, Muted);
                Stretch(count.rectTransform);
                count.rectTransform.offsetMax = new Vector2(-16, 0);
                y -= LayerRowHeight + 6;

                if (collapsed) { y -= Gap - 6; continue; }

                if (!string.IsNullOrEmpty(first.LayerDescription))
                {
                    var desc = Label(_navList, first.LayerDescription, 18, TextAnchor.UpperLeft, Muted);
                    Place(desc, new Vector2(0, 1), new Vector2(TreeIndent, y), new Vector2(width - TreeIndent, 28), new Vector2(0, 1));
                    y -= 32;
                }

                // Operation 줄 (파일) + 트리 선
                float lineTop = y + 6;
                float lastMid = y;
                foreach (ChapterEntry c in entries)
                {
                    float mid = y - OpRowHeight / 2;
                    lastMid = mid;
                    var branch = Panel(_navList, "Branch", TreeLine);
                    Place(branch, new Vector2(0, 1), new Vector2(22, mid + 1), new Vector2(TreeIndent - 26, 2), new Vector2(0, 1));
                    if (c.Frontier) _frontierRowTop = y;
                    OperationRow(c, new Vector2(TreeIndent, y), width - TreeIndent);
                    y -= OpRowHeight + 8;
                }
                var trunk = Panel(_navList, "Trunk", TreeLine);
                Place(trunk, new Vector2(0, 1), new Vector2(22, lineTop), new Vector2(2, lineTop - lastMid), new Vector2(0, 1));
                y -= Gap;
            }
            // 스크롤 길이 = 목록 전체 높이
            _navList.sizeDelta = new Vector2(0, -y);
        }

        private void OperationRow(ChapterEntry c, Vector2 pos, float width)
        {
            Color color = c.Current ? new Color32(0x1c, 0x3a, 0x52, 0xff)
                        : c.Selectable ? ButtonColor : new Color32(0x22, 0x26, 0x2e, 0xff);
            string id = c.Id;
            var row = Button(_navList, "", () => { CloseNavigator(); OnChapterSelected?.Invoke(id); }, 24, color);
            row.interactable = c.Selectable;
            Place(row, new Vector2(0, 1), pos, new Vector2(width, OpRowHeight), new Vector2(0, 1));
            FileIcon(row.transform, new Vector2(16, -18), c.Selectable);
            var rowText = row.GetComponentInChildren<Text>();
            rowText.alignment = TextAnchor.UpperLeft;
            rowText.fontSize = 26;
            rowText.text = c.Title + (c.Current ? "  · 현재" : "") + (c.Frontier ? "  · 진행 중" : "");
            rowText.rectTransform.offsetMin = new Vector2(56, 12);
            rowText.rectTransform.offsetMax = new Vector2(-16, -14);
            var sub = Label(row.transform, (string.IsNullOrEmpty(c.Subtitle) ? "" : c.Subtitle + "\n") + c.Status, 19,
                TextAnchor.LowerLeft, Muted);
            Stretch(sub.rectTransform);
            sub.rectTransform.offsetMin = new Vector2(56, 12);
            sub.rectTransform.offsetMax = new Vector2(-140, -12);
            if (c.HasStory)
            {
                var story = Button(row.transform, "스토리", () => { CloseNavigator(); OnChapterStory?.Invoke(id); }, 20);
                Place(story, new Vector2(1, 0), new Vector2(-12, 12), new Vector2(110, 48), new Vector2(1, 0));
            }
        }

        /// <summary>폴더 아이콘 (탭이 있는 노란 상자). 글꼴과 상관없이 보이게 UI 상자로 그린다</summary>
        private static void FolderIcon(Transform parent, Vector2 leftCenter)
        {
            var tab = Panel(parent, "FolderTab", FolderColor);
            Place(tab, new Vector2(0, 0.5f), leftCenter + new Vector2(0, 12), new Vector2(16, 8), new Vector2(0, 0.5f));
            tab.GetComponent<Image>().raycastTarget = false;
            var body = Panel(parent, "Folder", FolderColor);
            Place(body, new Vector2(0, 0.5f), leftCenter + new Vector2(0, -2), new Vector2(38, 26), new Vector2(0, 0.5f));
            body.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>파일 아이콘 (테두리 문서). 열려 있으면 밝게</summary>
        private static void FileIcon(Transform parent, Vector2 topLeft, bool active)
        {
            var page = Panel(parent, "File", active ? new Color32(0xe6, 0xee, 0xf6, 0xff) : new Color32(0x6b, 0x76, 0x86, 0xff));
            Place(page, new Vector2(0, 1), topLeft, new Vector2(26, 32), new Vector2(0, 1));
            page.GetComponent<Image>().raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                var line = Panel(page, "Line", new Color32(0x4a, 0x56, 0x68, 0xff));
                Place(line, new Vector2(0, 1), new Vector2(5, -8 - i * 7), new Vector2(16, 2), new Vector2(0, 1));
                line.GetComponent<Image>().raycastTarget = false;
            }
        }

        public void CloseNavigator(bool immediate = false)
        {
            _navOpen = false;
            if (_navDim != null) _navDim.SetActive(false);
            if (immediate && _nav != null) _nav.anchoredPosition = new Vector2(NavWidth + 20, 0);
        }

        private static void DashedLine(Transform parent, Vector2 a, Vector2 b, bool active)
        {
            Vector2 dir = (b - a).normalized;
            float length = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Color color = active ? Line : Muted;
            for (float d = 76; d < length - 76; d += 22)
            {
                var dash = Panel(parent, "Dash", color);
                Place(dash, new Vector2(0, 0), a + dir * d, new Vector2(12, 3), new Vector2(0.5f, 0.5f));
                dash.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }

        // ================= 팝업 =================
        public void ShowMessage(string title, string text)
        {
            Popup(_popupRoot, title, new Vector2(760, 360), out RectTransform body);
            var t = Label(body, text, 28, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
        }

        /// <summary>메인 화면 친구 버튼에 안 읽은 메시지 + 받은 친구 요청 수를 표시한다</summary>
        public void SetFriendCount(int count)
        {
            if (_friendButton == null) return;
            SetButtonText(_friendButton, count > 0 ? $"친구\n({count})" : "친구");
            var label = _friendButton.GetComponentInChildren<Text>();
            if (label != null) label.color = count > 0 ? Warn : TextColor;
        }

        /// <summary>메인 화면 우편 버튼에 받지 않은 우편 수를 표시한다 (0이면 "우편"만)</summary>
        public void SetMailCount(int unclaimed)
        {
            if (_mailButton == null) return;
            SetButtonText(_mailButton, unclaimed > 0 ? $"우편\n({unclaimed})" : "우편");
            var label = _mailButton.GetComponentInChildren<Text>();
            if (label != null) label.color = unclaimed > 0 ? Warn : TextColor;
        }

        /// <summary>우편함 팝업: 최신순 목록(누르면 자세히) + 모두 받기</summary>
        public void ShowMailbox(IReadOnlyList<Data.MailResponse> mails, Action<Data.MailResponse> onOpen, Action onClaimAll)
        {
            int unclaimed = mails.Count(m => !m.Claimed);
            Popup(_popupRoot, $"우편함 · 받지 않은 우편 {unclaimed}통", new Vector2(1100, 820), out RectTransform body);
            if (mails.Count == 0)
            {
                var empty = Label(body, "우편이 없습니다.", 28, TextAnchor.MiddleCenter, Muted);
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

        /// <summary>우편 자세히 팝업. 받지 않았으면 받기 버튼</summary>
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
                var done = Label(body, "이미 받은 우편입니다.", 24, TextAnchor.MiddleRight, Muted);
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

            // Enter: 이메일 → 비밀번호 → (회원가입이면 재입력 →) 제출
            OnEnter(email, () => FocusLater(password));
            OnEnter(password, () => { if (signupMode) FocusLater(confirm); else Submit(); });
            OnEnter(confirm, Submit);
            FocusLater(email);
        }

        /// <summary>한 프레임 뒤에 입력칸을 선택한다 (Enter로 입력을 마친 같은 프레임에는 선택이 무시될 수 있음)</summary>
        private void FocusLater(InputField field) => StartCoroutine(FocusRoutine(field));

        private static IEnumerator FocusRoutine(InputField field)
        {
            yield return null;
            if (field == null || !field.gameObject.activeInHierarchy) yield break;
            field.Select();
            field.ActivateInputField();
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
