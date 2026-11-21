using System;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 이야기 나레이션과 닉네임 입력 (시안 Prologue1·2, Nickname·NicknameError).
    /// 나레이션: 배경 그림(오류 로그 error / 일그러진 델타 창 glitch) 위 가운데 띠에 글, 오른쪽 위 건너뛰기, 왼쪽 아래 진행 점,
    /// 아래 "클릭 또는 Space ▼". 닉네임 입력: 격자 바탕 + "> 새로운 존재를 확인했습니다." + identify.exe 창(이름, 중복 확인, 확인)
    /// </summary>
    public partial class GameUI
    {
        /// <summary>닉네임 입력의 중복 확인 버튼, 확인 버튼</summary>
        public Action OnCheckNickname, OnSubmitNickname;
        /// <summary>나레이션 화면을 누르면 다음, 건너뛰기</summary>
        public Action OnNarrationSkip;

        private GameObject _narration, _narrationLine, _nameScreen;
        private Image _narrationBg;
        private Text _narrationText;
        private RectTransform _narrationDots;
        private InputField _nameInput;
        private Text _nameStatus;
        private Button _nameSubmit;
        private RectTransform _nameSubmitRt;
        private FieldFrame _nameFrame;
        /// <summary>나레이션 건너뛰기 버튼 (이 위의 클릭은 "다음"으로 치지 않는다)</summary>
        private RectTransform _narrationSkip;

        /// <summary>마우스가 나레이션 건너뛰기 버튼 위에 있는지</summary>
        private bool PointerOnNarrationSkip(Vector2 screen) =>
            _narration.activeSelf && _narrationLine.activeSelf && _narrationSkip != null
            && RectTransformUtility.RectangleContainsScreenPoint(_narrationSkip, screen, null);

        private void BuildNarration()
        {
            var screen = Fill(_root, "Narration", Hex(0x0a0716), raycast: true);
            Inset(screen, 0, 0, 0, 0);
            _narration = screen.gameObject;

            // ---- 나레이션 ----
            _narrationLine = Node(screen.transform, "Line").gameObject;
            Inset(_narrationLine.transform, 0, 0, 0, 0);
            Transform line = _narrationLine.transform;
            _narrationBg = Fill(line, "Background", Color.white);
            Inset(_narrationBg, 0, 0, 0, 0);
            // 화면 클릭은 WireframeGame의 입력(E·Space·Enter·클릭)이 한 번만 처리한다.
            // 여기에도 버튼을 달면 클릭 한 번에 두 단계가 넘어간다
            _narrationBg.raycastTarget = true;
            // 가운데 띠: 양 끝이 투명한 짙은 띠
            var band = new GameObject("Band", typeof(RectTransform), typeof(RawImage));
            band.transform.SetParent(line, false);
            var raw = band.GetComponent<RawImage>();
            raw.texture = BandTexture();
            raw.raycastTarget = false;
            RectTransform bandRt = raw.rectTransform;
            bandRt.anchorMin = new Vector2(0, 0.5f);
            bandRt.anchorMax = new Vector2(1, 0.5f);
            bandRt.sizeDelta = new Vector2(0, 136 * S);
            bandRt.anchoredPosition = new Vector2(0, -4 * S);
            _narrationText = Label(band.transform, "", 24 * S, Fg, TextAnchor.MiddleCenter);
            _narrationText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _narrationText.lineSpacing = 1.4f;
            Inset(_narrationText, 260 * S, 0, 260 * S, 0);
            // 건너뛰기
            RectTransform skip = Node(line, "Skip");
            AtRight(skip, 24 * S, 24 * S, 96 * S, 32 * S);
            _narrationSkip = skip;
            Image skipBg = Fill(skip, "Bg", Hex(0x0c0a18, 0.8f));
            Inset(skipBg, 0, 0, 0, 0);
            HoverArea(skip, "Hover", Hex(0x22d3ee, 0.12f), () => OnNarrationSkip?.Invoke(), out Image skipArea);
            Inset(skipArea, 0, 0, 0, 0);
            Border(skip, TrackLine, 2 * S);
            Text skipText = Label(skip, "건너뛰기 >>", 12 * S, Muted, TextAnchor.MiddleCenter, bold: true);
            Inset(skipText, 0, 0, 0, 0);
            // 진행 점
            _narrationDots = Node(line, "Dots");
            _narrationDots.anchorMin = _narrationDots.anchorMax = new Vector2(0, 0);
            _narrationDots.pivot = new Vector2(0, 0);
            _narrationDots.anchoredPosition = new Vector2(24 * S, 28 * S);
            _narrationDots.sizeDelta = new Vector2(400 * S, 8 * S);
            // 클릭 또는 Space ▼
            RectTransform hint = Node(line, "Hint");
            hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0);
            hint.pivot = new Vector2(0.5f, 0);
            hint.anchoredPosition = new Vector2(0, 24 * S);
            hint.sizeDelta = new Vector2(200 * S, 18 * S);
            Text h1 = Label(hint, "클릭 또는", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(h1, 30 * S, 0, 70 * S, 18 * S);
            Kbd(hint, "Space", out RectTransform space);
            At(space, 30 * S + h1.preferredWidth + 6 * S, 0, 46 * S, 18 * S);
            Text arrow = Label(hint, "▼", 12 * S, Cyan, TextAnchor.MiddleLeft);
            At(arrow, 30 * S + h1.preferredWidth + 6 * S + 46 * S + 6 * S, 0, 20 * S, 18 * S);
            UiAnimator.Blink(arrow);

            // ---- 닉네임 입력 ----
            _nameScreen = Node(screen.transform, "Name").gameObject;
            Inset(_nameScreen.transform, 0, 0, 0, 0);
            Transform name = _nameScreen.transform;
            var grid = new GameObject("Grid", typeof(RectTransform), typeof(RawImage));
            grid.transform.SetParent(name, false);
            var gridRaw = grid.GetComponent<RawImage>();
            gridRaw.texture = HudGridTexture();
            gridRaw.uvRect = new Rect(0, 0, 1920f / (32 * S), 1080f / (32 * S));
            Inset(gridRaw, 0, 0, 0, 0);
            Text sys = Label(name, "> 새로운 존재를 확인했습니다.\n> 신원 파악을 시작합니다.", 12 * S, Cyan, TextAnchor.UpperCenter, UiKit.Mono);
            sys.lineSpacing = 1.5f;
            At(sys, 0, 72 * S, 1280 * S, 44 * S);
            Text ask = Label(name, "당신의 이름은 무엇인가요?", 36 * S, Fg, TextAnchor.MiddleCenter, bold: true);
            At(ask, 0, 72 * S + 44 * S + 18 * S, 1280 * S, 44 * S);

            const float winW = 440;
            float bodyH = 20 * S + 16 * S + 6 * S + 44 * S + 12 * S + 16 * S + 12 * S + 38 * S + 12 * S + 44 * S + 20 * S;
            RectTransform win = Frame(name, "Identify", Ink, WindowLine, WinBorder, 6 * S);
            At(win, 420 * S, 250 * S, winW * S, WinBorder * 2 + WinTitleH + bodyH);
            Image bar = Fill(win, "TitleBar", TitleBar);
            At(bar, WinBorder, WinBorder, winW * S - WinBorder * 2, 32 * S);
            Image barLine = Fill(win, "TitleLine", Cyan);
            At(barLine, WinBorder, WinBorder + 32 * S, winW * S - WinBorder * 2, 2 * S);
            Text title = Label(bar.transform, "identify.exe", 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            Inset(title, 12 * S, 0, 0, 0);
            RectTransform body = Node(win, "Body");
            Inset(body, WinBorder, WinBorder + WinTitleH, WinBorder, WinBorder);
            float w = winW * S - WinBorder * 2 - 44 * S, y = 20 * S;
            Text label = Label(body, "이름 (닉네임)", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(label, 22 * S, y, w, 16 * S);
            y += 16 * S + 6 * S;
            _nameInput = UiKit.Input(body, "", limit: 20, size: (int)(24 * S));
            At(_nameInput, 22 * S, y, w - 96 * S - 8 * S, 44 * S);
            _nameFrame = FieldFrame.Apply(_nameInput);
            _nameInput.textComponent.font = UiKit.Regular;
            _nameInput.textComponent.rectTransform.offsetMin = new Vector2(12 * S, 0);
            _nameInput.onValueChanged.AddListener(_ => SetNicknameStatus("", true, neutral: true));
            UiKit.OnEnter(_nameInput, () => OnSubmitNickname?.Invoke());
            GhostButton(body, "중복 확인", 12 * S, () => OnCheckNickname?.Invoke(), out RectTransform check);
            At(check, 22 * S + w - 96 * S, y, 96 * S, 44 * S);
            y += 44 * S + 12 * S;
            _nameStatus = Label(body, "", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(_nameStatus, 22 * S, y, w, 16 * S);
            Text rule = Label(body, "2~20자", 12 * S, Dim, TextAnchor.MiddleRight);
            At(rule, 22 * S, y, w, 16 * S);
            y += 16 * S + 12 * S;
            Image info = Fill(body, "Info", Night);
            At(info, 22 * S, y, w, 38 * S);
            Text infoText = Label(info.transform, "게임 속 인물들이 이 이름으로 당신을 불러요.", 12 * S, Muted, TextAnchor.MiddleLeft);
            Inset(infoText, 12 * S, 0, 12 * S, 0);
            y += 38 * S + 12 * S;
            _nameSubmit = PrimaryButton(body, "확인", 24 * S, () => OnSubmitNickname?.Invoke(), out _nameSubmitRt);
            At(_nameSubmitRt, 22 * S, y, w, 44 * S);
            Text foot = Label(name, "이름은 나중에 프로필에서 바꿀 수 있어요", 12 * S, Dim, TextAnchor.MiddleCenter);
            foot.rectTransform.anchorMin = foot.rectTransform.anchorMax = new Vector2(0.5f, 0);
            foot.rectTransform.pivot = new Vector2(0.5f, 0);
            foot.rectTransform.anchoredPosition = new Vector2(0, 28 * S);
            foot.rectTransform.sizeDelta = new Vector2(800 * S, 16 * S);
        }

        /// <summary>나레이션 한 줄. background: "glitch"(일그러진 델타 창) 또는 그 밖(오류 로그)</summary>
        public void ShowNarration(string text, int index, int total, string background)
        {
            _narration.SetActive(true);
            _narrationLine.SetActive(true);
            _nameScreen.SetActive(false);
            Sprite bg = ArtCatalog.Illustration(background == "glitch" ? "narration_glitch" : "narration_error");
            _narrationBg.sprite = bg;
            _narrationBg.color = bg != null ? Color.white : Hex(0x0a0716);
            _narrationText.text = text;
            UiKit.Clear(_narrationDots);
            float x = 0;
            for (int i = 0; i < total; i++)
            {
                float dw = i == index ? 16 * S : 8 * S;
                Image dot = Fill(_narrationDots, "Dot", i <= index ? Cyan : TrackLine);
                At(dot, x, 0, dw, 8 * S);
                x += dw + 4 * S;
            }
        }

        /// <summary>닉네임 입력 화면 (identify.exe)</summary>
        public void ShowNameInput(string currentName)
        {
            _narration.SetActive(true);
            _narrationLine.SetActive(false);
            _nameScreen.SetActive(true);
            _nameInput.text = currentName ?? "";
            SetNicknameStatus("", true, neutral: true);
            _nameInput.Select();
            _nameInput.ActivateInputField();
        }

        /// <summary>닉네임 입력 아래 상태 문구 (사용 가능 초록 / 이미 사용 중·오류 빨강). 빨강이면 확인 버튼을 끈다</summary>
        public void SetNicknameStatus(string text, bool ok, bool neutral = false)
        {
            if (_nameStatus == null) return;
            _nameStatus.text = text;
            _nameStatus.color = neutral ? Muted : ok ? Green : Red;
            _nameFrame.State = neutral ? FieldFrame.Mark.None : ok ? FieldFrame.Mark.Good : FieldFrame.Mark.Bad;
            bool disabled = !neutral && !ok;
            _nameSubmit.interactable = !disabled;
            Transform shadow = _nameSubmitRt.Find("Shadow");
            if (shadow != null) shadow.gameObject.SetActive(!disabled);
            var colors = _nameSubmit.colors;
            colors.disabledColor = Track;
            _nameSubmit.colors = colors;
            var label = _nameSubmitRt.GetComponentInChildren<Text>();
            if (label != null) label.color = disabled ? Dim : Ink;
        }

        public string NameInput => _nameInput.text.Trim();

        public void HideNarration() => _narration.SetActive(false);

        private static Texture2D _bandTexture, _hudGrid;

        /// <summary>가로 띠: 0~20% 투명에서 짙게, 80~100% 다시 투명 (시안 linear-gradient)</summary>
        private static Texture2D BandTexture()
        {
            if (_bandTexture != null) return _bandTexture;
            const int n = 256;
            _bandTexture = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < n; x++)
            {
                float t = x / (float)(n - 1);
                float a = t < 0.2f ? t / 0.2f : t > 0.8f ? (1 - t) / 0.2f : 1f;
                _bandTexture.SetPixel(x, 0, new Color(10 / 255f, 7 / 255f, 22 / 255f, 0.92f * a));
            }
            _bandTexture.Apply();
            return _bandTexture;
        }

        /// <summary>격자 바탕 (#0a0716 + 32px 선 #110d24)</summary>
        private static Texture2D HudGridTexture()
        {
            if (_hudGrid != null) return _hudGrid;
            const int n = 32;
            _hudGrid = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[n * n];
            Color32 bg = Hex(0x0a0716), line = Hex(0x110d24);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                pixels[y * n + x] = x == 0 || y == n - 1 ? line : bg;
            _hudGrid.SetPixels32(pixels);
            _hudGrid.Apply();
            return _hudGrid;
        }
    }
}
