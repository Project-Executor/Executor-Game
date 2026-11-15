using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Executor.Play
{
    /// <summary>
    /// 와이어프레임 UI. 프리팹 없이 코드로 만든다 (기준 해상도 1920x1080).
    /// 대화창 1760x300, 이름표 280x60, 선택지 600x64, 스탠딩 일러스트 높이 1000.
    /// 디자인이 나오면 같은 역할의 프리팹으로 바꾸면 된다.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        private static readonly Color Panel = new Color32(0x10, 0x18, 0x24, 0xee);
        private static readonly Color Line = new Color32(0x4f, 0xd1, 0xff, 0xff);
        private static readonly Color TextColor = new Color32(0xe6, 0xee, 0xf6, 0xff);
        private static readonly Color Muted = new Color32(0x8c, 0x9b, 0xad, 0xff);

        private Font _font;
        private Font _bold;
        private RectTransform _root;

        // 스테이지 선택
        private GameObject _select;
        private Text _selectInfo;
        private RectTransform _stageList;

        // HUD
        private GameObject _hud;
        private Text _hudTitle, _hudStats;

        // 안내 문구
        private GameObject _toast;
        private Text _toastText;
        private readonly Queue<string> _toasts = new Queue<string>();
        private float _toastUntil;

        // 대화
        private GameObject _dialogue;
        private Image _portrait;
        private GameObject _portraitPlaceholder;
        private Text _portraitPlaceholderText;
        private Text _speaker, _line, _next;
        private RectTransform _choices;
        private string _fullLine;
        private float _typed;
        public bool IsTyping => _dialogue.activeSelf && _fullLine != null && (int)_typed < _fullLine.Length;

        // 나레이션
        private GameObject _narration;
        private Text _narrationText, _narrationHint;
        private InputField _nameInput;

        // 결과
        private GameObject _result;
        private Text _resultText;
        private RectTransform _resultButtons;

        public void Build()
        {
            // Pretendard (SIL Open Font License, Resources/Fonts/Pretendard-LICENSE.txt). 없으면 OS 한글 폰트
            Font os = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            _font = Resources.Load<Font>("Fonts/Pretendard-Regular") ?? os;
            _bold = Resources.Load<Font>("Fonts/Pretendard-Bold") ?? _font;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;

            BuildHud();
            BuildToast();
            BuildDialogue();
            BuildNarration();
            BuildResult();
            BuildSelect();
            HideAll();
        }

        public void HideAll()
        {
            _select.SetActive(false);
            _hud.SetActive(false);
            _toast.SetActive(false);
            _toasts.Clear();
            _dialogue.SetActive(false);
            _narration.SetActive(false);
            _result.SetActive(false);
        }

        // ---------------- 스테이지 선택 ----------------
        public void ShowSelect(string info, IEnumerable<(string label, Action onClick)> stages)
        {
            HideAll();
            _select.SetActive(true);
            _selectInfo.text = info;
            foreach (Transform child in _stageList) Destroy(child.gameObject);
            int i = 0;
            foreach (var (label, onClick) in stages)
            {
                var b = MakeButton(_stageList, label, onClick, 28);
                Place(b, new Vector2(0.5f, 1), new Vector2(0, -i * 84), new Vector2(900, 72));
                i++;
            }
        }

        // ---------------- HUD ----------------
        public void ShowHud(string title)
        {
            _hud.SetActive(true);
            _hudTitle.text = title;
        }

        public void SetStats(string text) => _hudStats.text = text;

        // ---------------- 안내 문구 ----------------
        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _toasts.Enqueue(text);
            if (!_toast.activeSelf) NextToast();
        }

        private void NextToast()
        {
            if (_toasts.Count == 0) { _toast.SetActive(false); return; }
            _toast.SetActive(true);
            _toastText.text = _toasts.Dequeue();
            _toastUntil = Time.unscaledTime + Mathf.Clamp(_toastText.text.Length * 0.08f, 2.5f, 6f);
        }

        // ---------------- 대화 ----------------
        public void ShowLine(string speaker, string text, Sprite portrait, bool portraitOnRight, string placeholderName)
        {
            _dialogue.SetActive(true);
            _speaker.text = speaker ?? "";
            _speaker.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            _fullLine = text ?? "";
            _typed = 0;
            _line.text = "";
            _next.gameObject.SetActive(false);
            ClearChoices();

            var rt = (RectTransform)_portrait.transform.parent;
            rt.anchorMin = rt.anchorMax = new Vector2(portraitOnRight ? 1 : 0, 0);
            rt.pivot = new Vector2(portraitOnRight ? 1 : 0, 0);
            rt.anchoredPosition = new Vector2(portraitOnRight ? -120 : 120, 0);
            _portrait.gameObject.SetActive(portrait != null);
            _portraitPlaceholder.SetActive(portrait == null && placeholderName != null);
            if (portrait != null)
            {
                _portrait.sprite = portrait;
                float h = 1000f;
                rt.sizeDelta = new Vector2(h * portrait.rect.width / portrait.rect.height, h);
            }
            else
            {
                rt.sizeDelta = new Vector2(600, 1000);
                _portraitPlaceholderText.text = $"{placeholderName}\n(일러스트 없음)";
            }
        }

        /// <summary>글자를 다 보여 준다 (출력 중 E를 누른 경우)</summary>
        public void CompleteLine() => _typed = _fullLine?.Length ?? 0;

        public void ShowChoices(IReadOnlyList<string> choices, Action<int> onChoose)
        {
            ClearChoices();
            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                var b = MakeButton(_choices, $"{i + 1}. {choices[i]}", () => onChoose(index), 28);
                Place(b, new Vector2(1, 0), new Vector2(0, (choices.Count - 1 - i) * 76), new Vector2(600, 64), new Vector2(1, 0));
            }
        }

        public void HideDialogue() => _dialogue.SetActive(false);

        private void ClearChoices()
        {
            foreach (Transform child in _choices) Destroy(child.gameObject);
        }

        // ---------------- 나레이션 ----------------
        public void ShowNarration(string text, bool askName, string currentName)
        {
            _narration.SetActive(true);
            _narrationText.text = text;
            _nameInput.gameObject.SetActive(askName);
            _narrationHint.text = askName ? "이름을 입력하고 Enter (1~20자)" : "E / Space / 클릭: 다음";
            if (askName)
            {
                _nameInput.text = currentName ?? "";
                _nameInput.Select();
                _nameInput.ActivateInputField();
            }
        }

        public string NameInput => _nameInput.text.Trim();

        public void HideNarration() => _narration.SetActive(false);

        // ---------------- 결과 ----------------
        public void ShowResult(string text, IEnumerable<(string label, Action onClick)> buttons)
        {
            _result.SetActive(true);
            _resultText.text = text;
            foreach (Transform child in _resultButtons) Destroy(child.gameObject);
            int i = 0;
            foreach (var (label, onClick) in buttons)
            {
                var b = MakeButton(_resultButtons, label, onClick, 28);
                Place(b, new Vector2(0, 0.5f), new Vector2(i * 270, 0), new Vector2(250, 72), new Vector2(0, 0.5f));
                i++;
            }
        }

        public void HideResult() => _result.SetActive(false);

        private void Update()
        {
            if (_dialogue.activeSelf && _fullLine != null && (int)_typed < _fullLine.Length)
            {
                _typed += Time.unscaledDeltaTime * 40f; // 초당 40자
                _line.text = _fullLine.Substring(0, Mathf.Min((int)_typed, _fullLine.Length));
            }
            else if (_dialogue.activeSelf && _fullLine != null)
            {
                _line.text = _fullLine;
                _next.gameObject.SetActive(_choices.childCount == 0);
            }
            if (_toast.activeSelf && Time.unscaledTime > _toastUntil) NextToast();
        }

        // ---------------- 만들기 ----------------
        private void BuildSelect()
        {
            _select = MakePanel(_root, "StageSelect", new Color32(0x0b, 0x12, 0x1c, 0xff)).gameObject;
            Stretch((RectTransform)_select.transform);
            var title = MakeText(_select.transform, "EXECUTOR · 튜토리얼 (와이어프레임)", 48, TextAnchor.MiddleCenter, bold: true);
            Place(title, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1400, 80));
            _selectInfo = MakeText(_select.transform, "", 26, TextAnchor.MiddleCenter, Muted);
            Place(_selectInfo, new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(1400, 50));
            _stageList = new GameObject("Stages", typeof(RectTransform)).GetComponent<RectTransform>();
            _stageList.SetParent(_select.transform, false);
            Place(_stageList, new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(900, 520));
            var help = MakeText(_select.transform, "숫자 1~6: 스테이지 선택 · N: 집행자 이름 초기화", 24, TextAnchor.MiddleCenter, Muted);
            Place(help, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(1400, 40));
        }

        private void BuildHud()
        {
            _hud = new GameObject("Hud", typeof(RectTransform));
            _hud.transform.SetParent(_root, false);
            Stretch((RectTransform)_hud.transform);
            _hudTitle = MakeText(_hud.transform, "", 30, TextAnchor.MiddleLeft, bold: true);
            Place(_hudTitle, new Vector2(0, 1), new Vector2(40, -20), new Vector2(900, 50), new Vector2(0, 1));
            _hudStats = MakeText(_hud.transform, "", 30, TextAnchor.MiddleRight);
            Place(_hudStats, new Vector2(1, 1), new Vector2(-40, -20), new Vector2(700, 50), new Vector2(1, 1));
            var help = MakeText(_hud.transform, "W A S D 이동 · E 대화 · R 다시 · Esc 목록", 24, TextAnchor.MiddleLeft, Muted);
            Place(help, new Vector2(0, 0), new Vector2(40, 16), new Vector2(900, 40), new Vector2(0, 0));
        }

        private void BuildToast()
        {
            var panel = MakePanel(_root, "Toast", Panel, Line);
            Place(panel, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1200, 110));
            _toast = panel.gameObject;
            _toastText = MakeText(panel, "", 28, TextAnchor.MiddleCenter);
            Stretch(_toastText.rectTransform, 24);
        }

        private void BuildDialogue()
        {
            _dialogue = new GameObject("Dialogue", typeof(RectTransform));
            _dialogue.transform.SetParent(_root, false);
            Stretch((RectTransform)_dialogue.transform);

            // 스탠딩 일러스트 (대화창 뒤)
            var portraitRoot = new GameObject("Portrait", typeof(RectTransform)).GetComponent<RectTransform>();
            portraitRoot.SetParent(_dialogue.transform, false);
            _portrait = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _portrait.transform.SetParent(portraitRoot, false);
            _portrait.preserveAspect = true;
            Stretch(_portrait.rectTransform);
            var ph = MakePanel(portraitRoot, "Placeholder", new Color32(0x20, 0x2c, 0x3c, 0xcc), Muted);
            Stretch(ph);
            _portraitPlaceholder = ph.gameObject;
            _portraitPlaceholderText = MakeText(ph, "", 32, TextAnchor.MiddleCenter, Muted);
            Stretch(_portraitPlaceholderText.rectTransform);

            // 대화창 1760x300, 아래에서 40
            var box = MakePanel(_dialogue.transform, "Box", Panel, Line);
            Place(box, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1760, 300), new Vector2(0.5f, 0));
            _line = MakeText(box, "", 34, TextAnchor.UpperLeft);
            _line.lineSpacing = 1.3f;
            Stretch(_line.rectTransform, 0);
            _line.rectTransform.offsetMin = new Vector2(56, 40);
            _line.rectTransform.offsetMax = new Vector2(-56, -40);
            _next = MakeText(box, "▼", 30, TextAnchor.LowerRight, Line);
            Place(_next, new Vector2(1, 0), new Vector2(-36, 20), new Vector2(60, 40), new Vector2(1, 0));

            // 이름표 280x60, 대화창 왼쪽 위
            var plate = MakePanel(box, "NamePlate", new Color32(0x1c, 0x3a, 0x52, 0xff), Line);
            Place(plate, new Vector2(0, 1), new Vector2(40, -30), new Vector2(280, 60), new Vector2(0, 0));
            _speaker = MakeText(plate, "", 30, TextAnchor.MiddleCenter, bold: true);
            Stretch(_speaker.rectTransform);

            // 선택지 (대화창 오른쪽 위)
            _choices = new GameObject("Choices", typeof(RectTransform)).GetComponent<RectTransform>();
            _choices.SetParent(_dialogue.transform, false);
            Place(_choices, new Vector2(1, 0), new Vector2(-80, 360), new Vector2(600, 400), new Vector2(1, 0));
        }

        private void BuildNarration()
        {
            var panel = MakePanel(_root, "Narration", new Color(0, 0, 0, 0.92f));
            Stretch(panel);
            _narration = panel.gameObject;
            _narrationText = MakeText(panel, "", 40, TextAnchor.MiddleCenter);
            _narrationText.lineSpacing = 1.3f;
            Place(_narrationText, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1500, 400));
            _narrationHint = MakeText(panel, "", 24, TextAnchor.MiddleCenter, Muted);
            Place(_narrationHint, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(1200, 40));

            var inputBg = MakePanel(panel, "NameInput", new Color32(0x1c, 0x26, 0x34, 0xff), Line);
            Place(inputBg, new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(600, 80));
            var inputText = MakeText(inputBg, "", 36, TextAnchor.MiddleCenter);
            inputText.supportRichText = false;
            Stretch(inputText.rectTransform, 12);
            var placeholder = MakeText(inputBg, "집행자 이름", 36, TextAnchor.MiddleCenter, Muted);
            Stretch(placeholder.rectTransform, 12);
            _nameInput = inputBg.gameObject.AddComponent<InputField>();
            _nameInput.textComponent = inputText;
            _nameInput.placeholder = placeholder;
            _nameInput.characterLimit = 20;
        }

        private void BuildResult()
        {
            var panel = MakePanel(_root, "Result", Panel, Line);
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 460));
            _result = panel.gameObject;
            _resultText = MakeText(panel, "", 36, TextAnchor.MiddleCenter, bold: true);
            Place(_resultText, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(820, 260), new Vector2(0.5f, 1));
            _resultButtons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            _resultButtons.SetParent(panel, false);
            Place(_resultButtons, new Vector2(0.5f, 0), new Vector2(-395, 60), new Vector2(790, 72), new Vector2(0, 0));
        }

        // ---------------- 도우미 ----------------
        private RectTransform MakePanel(Transform parent, string name, Color color, Color? border = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            if (border != null)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = border.Value;
                outline.effectDistance = new Vector2(3, -3);
            }
            return (RectTransform)go.transform;
        }

        private Text MakeText(Transform parent, string text, int size, TextAnchor align, Color? color = null, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = bold ? _bold : _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private RectTransform MakeButton(Transform parent, string label, Action onClick, int size)
        {
            var rt = MakePanel(parent, "Button", new Color32(0x1c, 0x2c, 0x40, 0xff), Line);
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.75f, 0.9f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            var t = MakeText(rt, label, size, TextAnchor.MiddleCenter, bold: true);
            Stretch(t.rectTransform, 8);
            return rt;
        }

        private static void Place(Component c, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, anchor.y);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
