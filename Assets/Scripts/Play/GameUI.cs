using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.UiKit;

namespace Executor.Play
{
    /// <summary>
    /// 게임 화면 UI (와이어프레임 stage(ex) 배치, 기준 1920x1080).
    /// <list type="bullet">
    ///   <item>왼쪽 위: 남은 턴 / 그 아래: 퀘스트(안내 문구)</item>
    ///   <item>오른쪽 위: 수집, 채팅, 설정</item>
    ///   <item>가운데: 게임 화면 (BoardView가 GameArea 안에 맞춰 그린다)</item>
    ///   <item>대화창 1760x300, 이름표 280x60, 선택지 600x64, 스탠딩 일러스트 높이 1000</item>
    /// </list>
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        /// <summary>게임 화면 영역 (화면 비율 0~1, 왼쪽 아래 기준). 왼쪽 패널과 위쪽 버튼을 피한다</summary>
        public static readonly Rect GameArea = new Rect(400f / 1920, 60f / 1080, 1460f / 1920, 900f / 1080);

        private RectTransform _root;
        private RectTransform _popupRoot;

        // HUD
        private GameObject _hud;
        private Text _stageTitle, _turns, _keys, _quest;
        private Image _turnsBox;
        private Button _collect;

        // 안내
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

        public Action OnChat, OnSettings, OnCollect;

        public bool HasPopup => _popupRoot.childCount > 0;

        public void Build()
        {
            _root = Canvas(transform, "GameCanvas", 10);
            BuildHud();
            BuildToast();
            BuildDialogue();
            BuildNarration();
            BuildResult();
            _popupRoot = new GameObject("Popups", typeof(RectTransform)).GetComponent<RectTransform>();
            _popupRoot.SetParent(_root, false);
            Stretch(_popupRoot);
            HideAll();
        }

        public void HideAll()
        {
            _hud.SetActive(false);
            _toast.SetActive(false);
            _toasts.Clear();
            _dialogue.SetActive(false);
            _narration.SetActive(false);
            _result.SetActive(false);
            ClosePopups();
        }

        public void ClosePopups() => Clear(_popupRoot);

        // ---------------- HUD ----------------
        public void ShowHud(string title, string quest, int collected, int collectTotal)
        {
            _hud.SetActive(true);
            _stageTitle.text = title;
            SetQuest(quest);
            SetButtonText(_collect, $"수집 {collected}/{collectTotal}");
        }

        /// <summary>남은 턴 표시. 0이면 주의 색, 음수면 실패 색. 제한이 없으면 ∞</summary>
        public void SetTurns(int? remaining, int keys)
        {
            _turns.text = remaining?.ToString() ?? "∞";
            _turns.color = remaining < 0 ? Bad : remaining == 0 ? Warn : TextColor;
            _turnsBox.color = remaining < 0 ? (Color)new Color32(0x3a, 0x14, 0x1c, 0xee) : PanelColor;
            _keys.text = keys > 0 ? $"열쇠 {keys}" : "";
        }

        public void SetQuest(string text) => _quest.text = text;

        /// <summary>게임 화면 위 팝업 메뉴 (일시 정지 등). 버튼을 누르면 닫히고 동작한다</summary>
        public void ShowMenu(string title, IEnumerable<(string label, Action onClick)> buttons)
        {
            Popup(_popupRoot, title, new Vector2(620, 520), out RectTransform body);
            float y = 0;
            foreach (var (label, onClick) in buttons)
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
            Clear(_choices);

            var rt = (RectTransform)_portrait.transform.parent;
            rt.anchorMin = rt.anchorMax = new Vector2(portraitOnRight ? 1 : 0, 0);
            rt.pivot = new Vector2(portraitOnRight ? 1 : 0, 0);
            rt.anchoredPosition = new Vector2(portraitOnRight ? -120 : 120, 0);
            _portrait.gameObject.SetActive(portrait != null);
            _portraitPlaceholder.SetActive(portrait == null && placeholderName != null);
            if (portrait != null)
            {
                _portrait.sprite = portrait;
                rt.sizeDelta = new Vector2(1000f * portrait.rect.width / portrait.rect.height, 1000f);
            }
            else
            {
                rt.sizeDelta = new Vector2(600, 1000);
                _portraitPlaceholderText.text = $"{placeholderName}\n(일러스트 없음)";
            }
        }

        public void CompleteLine() => _typed = _fullLine?.Length ?? 0;

        public void ShowChoices(IReadOnlyList<string> choices, Action<int> onChoose)
        {
            Clear(_choices);
            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                var b = Button(_choices, $"{i + 1}. {choices[i]}", () => onChoose(index), 26);
                Place(b, new Vector2(1, 0), new Vector2(0, (choices.Count - 1 - i) * 76), new Vector2(600, 64), new Vector2(1, 0));
            }
        }

        public void HideDialogue() => _dialogue.SetActive(false);

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

        public void SetNarrationHint(string text) => _narrationHint.text = text;

        public string NameInput => _nameInput.text.Trim();

        public void HideNarration() => _narration.SetActive(false);

        // ---------------- 결과 ----------------
        public void ShowResult(string text, bool success, IEnumerable<(string label, Action onClick)> buttons)
        {
            _result.SetActive(true);
            _resultText.text = text;
            _resultText.color = success ? TextColor : Bad;
            Clear(_resultButtons);
            int i = 0;
            foreach (var (label, onClick) in buttons)
            {
                var b = Button(_resultButtons, label, onClick, 26);
                Place(b, new Vector2(0, 0.5f), new Vector2(i * 270, 0), new Vector2(250, 72), new Vector2(0, 0.5f));
                i++;
            }
        }

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
        private void BuildHud()
        {
            _hud = new GameObject("Hud", typeof(RectTransform));
            _hud.transform.SetParent(_root, false);
            Stretch((RectTransform)_hud.transform);

            // 남은 턴 (왼쪽 위)
            var box = Panel(_hud.transform, "Turns", PanelColor, Line);
            Place(box, new Vector2(0, 1), new Vector2(40, -40), new Vector2(320, 170), new Vector2(0, 1));
            _turnsBox = box.GetComponent<Image>();
            var label = Label(box, "남은 턴", 24, TextAnchor.UpperLeft, Muted);
            Stretch(label.rectTransform, 18);
            _turns = Label(box, "", 80, TextAnchor.MiddleCenter, bold: true);
            Stretch(_turns.rectTransform);
            _turns.rectTransform.offsetMax = new Vector2(0, -24);
            _keys = Label(box, "", 22, TextAnchor.LowerRight, new Color32(0xff, 0xd2, 0x3f, 0xff));
            Stretch(_keys.rectTransform, 14);

            // 퀘스트 (그 아래)
            var quest = Panel(_hud.transform, "Quest", PanelColor, Line);
            Place(quest, new Vector2(0, 1), new Vector2(40, -240), new Vector2(320, 360), new Vector2(0, 1));
            var qTitle = Label(quest, "퀘스트", 26, TextAnchor.UpperLeft, bold: true);
            Stretch(qTitle.rectTransform, 18);
            _quest = Label(quest, "", 22, TextAnchor.UpperLeft);
            _quest.lineSpacing = 1.25f;
            Stretch(_quest.rectTransform, 18);
            _quest.rectTransform.offsetMax = new Vector2(-18, -64);

            // 스테이지 이름 (위쪽, 게임 화면 가운데)
            _stageTitle = Label(_hud.transform, "", 28, TextAnchor.MiddleCenter, Muted, bold: true);
            Place(_stageTitle, new Vector2(0.5f, 1), new Vector2(170, -30), new Vector2(900, 40));

            // 수집 / 채팅 / 설정 (오른쪽 위)
            var settings = Button(_hud.transform, "설정", () => OnSettings?.Invoke(), 22);
            Place(settings, new Vector2(1, 1), new Vector2(-40, -24), new Vector2(100, 56), new Vector2(1, 1));
            var chat = Button(_hud.transform, "채팅", () => OnChat?.Invoke(), 22);
            Place(chat, new Vector2(1, 1), new Vector2(-152, -24), new Vector2(100, 56), new Vector2(1, 1));
            _collect = Button(_hud.transform, "수집 0/0", () => OnCollect?.Invoke(), 22);
            Place(_collect, new Vector2(1, 1), new Vector2(-264, -24), new Vector2(160, 56), new Vector2(1, 1));

            var help = Label(_hud.transform, "W A S D 이동 · E 대화 · R 다시 · Esc 메뉴", 22, TextAnchor.MiddleLeft, Muted);
            Place(help, new Vector2(0, 0), new Vector2(40, 16), new Vector2(900, 36), new Vector2(0, 0));
        }

        private void BuildToast()
        {
            var panel = Panel(_root, "Toast", PanelColor, Line);
            Place(panel, new Vector2(0.5f, 1), new Vector2(170, -100), new Vector2(1000, 100));
            _toast = panel.gameObject;
            _toastText = Label(panel, "", 26, TextAnchor.MiddleCenter);
            Stretch(_toastText.rectTransform, 20);
        }

        private void BuildDialogue()
        {
            _dialogue = new GameObject("Dialogue", typeof(RectTransform));
            _dialogue.transform.SetParent(_root, false);
            Stretch((RectTransform)_dialogue.transform);

            var portraitRoot = new GameObject("Portrait", typeof(RectTransform)).GetComponent<RectTransform>();
            portraitRoot.SetParent(_dialogue.transform, false);
            _portrait = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _portrait.transform.SetParent(portraitRoot, false);
            _portrait.preserveAspect = true;
            Stretch(_portrait.rectTransform);
            var ph = Panel(portraitRoot, "Placeholder", new Color32(0x20, 0x2c, 0x3c, 0xcc), Muted);
            Stretch(ph);
            _portraitPlaceholder = ph.gameObject;
            _portraitPlaceholderText = Label(ph, "", 32, TextAnchor.MiddleCenter, Muted);
            Stretch(_portraitPlaceholderText.rectTransform);

            var box = Panel(_dialogue.transform, "Box", PanelColor, Line);
            Place(box, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1760, 300), new Vector2(0.5f, 0));
            _line = Label(box, "", 34, TextAnchor.UpperLeft);
            _line.lineSpacing = 1.3f;
            Stretch(_line.rectTransform);
            _line.rectTransform.offsetMin = new Vector2(56, 40);
            _line.rectTransform.offsetMax = new Vector2(-56, -40);
            _next = Label(box, "▼", 30, TextAnchor.LowerRight, Line);
            Place(_next, new Vector2(1, 0), new Vector2(-36, 20), new Vector2(60, 40), new Vector2(1, 0));

            var plate = Panel(box, "NamePlate", new Color32(0x1c, 0x3a, 0x52, 0xff), Line);
            Place(plate, new Vector2(0, 1), new Vector2(40, -30), new Vector2(280, 60), new Vector2(0, 0));
            _speaker = Label(plate, "", 30, TextAnchor.MiddleCenter, bold: true);
            Stretch(_speaker.rectTransform);

            _choices = new GameObject("Choices", typeof(RectTransform)).GetComponent<RectTransform>();
            _choices.SetParent(_dialogue.transform, false);
            Place(_choices, new Vector2(1, 0), new Vector2(-80, 360), new Vector2(600, 400), new Vector2(1, 0));
        }

        private void BuildNarration()
        {
            var panel = Panel(_root, "Narration", new Color(0, 0, 0, 0.92f));
            Stretch(panel);
            _narration = panel.gameObject;
            _narrationText = Label(panel, "", 40, TextAnchor.MiddleCenter);
            _narrationText.lineSpacing = 1.3f;
            Place(_narrationText, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1500, 400));
            _narrationHint = Label(panel, "", 24, TextAnchor.MiddleCenter, Muted);
            Place(_narrationHint, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(1200, 40));
            _nameInput = Input(panel, "집행자 이름", limit: 20, size: 36);
            _nameInput.textComponent.alignment = TextAnchor.MiddleCenter;
            ((Text)_nameInput.placeholder).alignment = TextAnchor.MiddleCenter;
            Place(_nameInput, new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(600, 80));
        }

        private void BuildResult()
        {
            var panel = Panel(_root, "Result", PanelColor, Line);
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 460));
            _result = panel.gameObject;
            _resultText = Label(panel, "", 36, TextAnchor.MiddleCenter, bold: true);
            Place(_resultText, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(820, 260), new Vector2(0.5f, 1));
            _resultButtons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            _resultButtons.SetParent(panel, false);
            Place(_resultButtons, new Vector2(0.5f, 0), new Vector2(-395, 60), new Vector2(790, 72), new Vector2(0, 0));
        }
    }
}
