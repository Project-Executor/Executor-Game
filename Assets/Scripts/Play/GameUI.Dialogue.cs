using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 대화창 (시안 DialogueLine / DialogueChoice). 화면을 어둡게 덮고 주인공은 왼쪽, 상대는 오른쪽에 큰 일러스트
    /// (말하지 않는 쪽은 어둡게). 아래 대사 상자에 이름표(말하는 쪽), 반대편 위에 로그·자동·건너뛰기.
    /// 선택지는 대사 상자 위 가운데에 세로로 (W/S·↑↓로 고르고 Enter, 숫자, 클릭)
    /// </summary>
    public partial class GameUI
    {
        private GameObject _dialogue;
        private Image _portraitLeft, _portraitRight;
        private RectTransform _dialogueBox, _choices, _dialogueButtons;
        private Text _line;
        private string _fullLine;
        private float _typed;
        private bool _speakerOnRight;

        // 선택지
        private readonly List<string> _choiceTexts = new List<string>();
        private Action<int> _onChoose;
        private int _choiceSelected;

        // 로그·자동
        private readonly List<(string speaker, string text, Color tag)> _dialogueLog = new List<(string, string, Color)>();
        private static bool _autoPlay;
        private float _autoWait;
        private bool _logOpen;

        /// <summary>자동 진행: 다음 대사 (클릭·키 입력은 화면이 직접 처리한다)</summary>
        public Action OnDialogueNext;
        /// <summary>건너뛰기: 선택지나 대화 끝까지</summary>
        public Action OnDialogueSkip;

        public bool IsTyping => _dialogue.activeSelf && _fullLine != null && (int)_typed < _fullLine.Length;
        public bool IsLogOpen => _logOpen && _popupRoot.childCount > 0;
        public bool HasChoicesShown => _dialogue.activeSelf && _choiceTexts.Count > 0;

        /// <summary>말하지 않는 쪽 일러스트 (brightness .4 saturate .6 흉내)</summary>
        private static readonly Color Unlit = new Color(0.36f, 0.36f, 0.4f, 1f);

        private void BuildDialogue()
        {
            _dialogue = new GameObject("Dialogue", typeof(RectTransform));
            _dialogue.transform.SetParent(_root, false);
            Inset((RectTransform)_dialogue.transform, 0, 0, 0, 0);

            Image dim = Fill(_dialogue.transform, "Dim", Hex(0x0a0716, 0.72f));
            Inset(dim, 0, 0, 0, 0);
            _portraitLeft = Portrait("PortraitLeft");
            _portraitRight = Portrait("PortraitRight");

            _choices = Node(_dialogue.transform, "Choices");
            Inset(_choices, 0, 0, 0, 0);
            _dialogueBox = Node(_dialogue.transform, "Box");
            At(_dialogueBox, 120 * S, 530 * S, 1040 * S, 160 * S);
        }

        private Image Portrait(string name)
        {
            Image image = Fill(_dialogue.transform, name, Color.white);
            image.preserveAspect = true;
            image.gameObject.SetActive(false);
            return image;
        }

        /// <summary>대화 하나를 시작할 때: 로그를 비우고, 처음부터 보일 일러스트(주인공·상대)를 둔다</summary>
        public void BeginDialogue(Sprite left, Sprite right)
        {
            _dialogueLog.Clear();
            SetPortrait(_portraitLeft, left, false);
            SetPortrait(_portraitRight, right, true);
        }

        private static void SetPortrait(Image image, Sprite sprite, bool right)
        {
            image.gameObject.SetActive(sprite != null);
            if (sprite == null) return;
            image.sprite = sprite;
            float h = 860 * S, w = h * sprite.rect.width / sprite.rect.height;
            if (right) AtRight(image, 10 * S, 60 * S, w, h);
            else At(image, 30 * S, 60 * S, w, h);
        }

        /// <summary>
        /// 대사 한 줄. portraitOnRight면 상대(오른쪽), 아니면 주인공(왼쪽)이 말한다. portrait가 없으면 그쪽 일러스트는 그대로.
        /// tagColor: 이름표 바탕색 (관리자 웹 '인물')
        /// </summary>
        public void ShowLine(string speaker, string text, Sprite portrait, bool portraitOnRight, Color tagColor)
        {
            _dialogue.SetActive(true);
            _speakerOnRight = portraitOnRight;
            if (portrait != null) SetPortrait(portraitOnRight ? _portraitRight : _portraitLeft, portrait, portraitOnRight);
            _portraitRight.color = portraitOnRight ? Color.white : Unlit;
            _portraitLeft.color = portraitOnRight ? Unlit : Color.white;

            _fullLine = text ?? "";
            _typed = 0;
            _autoWait = 0;
            _choiceTexts.Clear();
            _onChoose = null;
            UiKit.Clear(_choices);
            _dialogueLog.Add((speaker, _fullLine, tagColor));
            DrawDialogueBox(speaker, tagColor);
        }

        private void DrawDialogueBox(string speaker, Color tagColor)
        {
            UiKit.Clear(_dialogueBox);
            RectTransform frame = Frame(_dialogueBox, "Frame", Hex(0x0c0a18, 0.96f), WindowLine, 2 * S, 6 * S);
            Inset(frame, 0, 0, 0, 0);

            _line = Label(frame, "", 24 * S, Fg, TextAnchor.UpperLeft);
            _line.horizontalOverflow = HorizontalWrapMode.Wrap;
            _line.verticalOverflow = VerticalWrapMode.Overflow;
            _line.lineSpacing = 40f / 28f;
            _line.raycastTarget = false;
            Inset(_line, 32 * S, 40 * S, 32 * S, 0);
            Text arrow = Label(frame, "▼", 24 * S, Cyan, TextAnchor.LowerRight);
            AtRight(arrow, 24 * S, 160 * S - 18 * S - 28 * S, 40 * S, 28 * S);
            arrow.raycastTarget = false;
            UiAnimator.Blink(arrow);
            arrow.gameObject.SetActive(false);
            _nextArrow = arrow.gameObject;
            BorderOnTop(frame);

            // 이름표: 말하는 쪽 위 모서리
            if (!string.IsNullOrEmpty(speaker))
            {
                RectTransform tag = Frame(frame, "NameTag", tagColor, Ink, 2 * S, 0);
                Text name = Label(tag, speaker, 24 * S, Ink, TextAnchor.MiddleCenter, bold: true);
                Inset(name, 0, 0, 0, 0);
                float w = name.preferredWidth + 40 * S + 4 * S;
                if (_speakerOnRight) AtRight(tag, 28 * S, -22 * S, w, 40 * S);
                else At(tag, 28 * S, -22 * S, w, 40 * S);
                BorderOnTop(tag);
            }

            // 로그 · 자동 · 건너뛰기: 이름표 반대편 위
            _dialogueButtons = Node(frame, "Buttons");
            float x = 0;
            var buttons = new (string label, Action act, bool on)[]
            {
                ("로그", ToggleDialogueLog, false),
                ("자동", ToggleAuto, _autoPlay),
                ("건너뛰기", () => OnDialogueSkip?.Invoke(), false),
            };
            foreach (var b in buttons)
            {
                RectTransform btn = Node(_dialogueButtons, "Button:" + b.label);
                HoverArea(btn, "Hover", Hex(0x22d3ee, 0.10f), b.act, out Image area);
                Inset(area, 0, 0, 0, 0);
                Text t = Label(btn, b.label, 12 * S, b.on ? Cyan : Muted, TextAnchor.MiddleCenter, bold: true);
                Inset(t, 0, 0, 0, 0);
                float w = t.preferredWidth + 20 * S;
                At(btn, x, 0, w, 28 * S);
                x += w;
            }
            if (_speakerOnRight) At(_dialogueButtons, 12 * S, 8 * S, x, 28 * S);
            else AtRight(_dialogueButtons, 12 * S, 8 * S, x, 28 * S);
        }

        private GameObject _nextArrow;

        public void CompleteLine() => _typed = _fullLine?.Length ?? 0;

        public void ShowChoices(IReadOnlyList<string> choices, Action<int> onChoose)
        {
            _choiceTexts.Clear();
            _choiceTexts.AddRange(choices);
            _onChoose = onChoose;
            _choiceSelected = 0;
            DrawChoices();
        }

        private void DrawChoices()
        {
            UiKit.Clear(_choices);
            int n = _choiceTexts.Count;
            const float w = 520 * S, h = 52 * S, gap = 10 * S, bottom = 506 * S;
            for (int i = 0; i < n; i++)
            {
                int index = i;
                bool selected = i == _choiceSelected;
                float top = bottom - (n - i) * h - (n - 1 - i) * gap;
                RectTransform row = Frame(_choices, "Choice" + (i + 1), selected ? Cyan : Ink, selected ? Cyan : WindowLine, 2 * S, 4 * S);
                At(row, 380 * S, top, w, h);
                Image hit = Fill(row, "Hit", new Color(0, 0, 0, 0), raycast: true);
                Inset(hit, 0, 0, 0, 0);
                Clickable(hit, () => PickChoice(index), new Color(1, 1, 1, 0), new Color(1, 1, 1, 0));
                // 마우스를 올린 선택지가 고른 것(하늘색)이 된다. 같은 칸이면 다시 그리지 않는다 (새로 그린 칸에도 enter가 오므로)
                var trigger = hit.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ =>
                {
                    if (_choiceSelected == index) return;
                    _choiceSelected = index;
                    DrawChoices();
                });
                trigger.triggers.Add(enter);
                Text num = Label(row, (i + 1).ToString(), 12 * S, selected ? Ink : Fg, TextAnchor.MiddleLeft, UiKit.Mono);
                num.color = new Color(num.color.r, num.color.g, num.color.b, 0.7f);
                At(num, 20 * S, 0, 20 * S, h);
                Text t = Label(row, _choiceTexts[i], 24 * S, selected ? Ink : Fg, TextAnchor.MiddleLeft);
                At(t, 20 * S + 12 * S + 8 * S, 0, w - 60 * S, h);
                BorderOnTop(row);
            }
        }

        /// <summary>W/S·↑↓: 선택지 사이를 옮긴다</summary>
        public void MoveChoice(int step)
        {
            if (_choiceTexts.Count == 0) return;
            _choiceSelected = (_choiceSelected + step + _choiceTexts.Count) % _choiceTexts.Count;
            DrawChoices();
        }

        public void ConfirmChoice() => PickChoice(_choiceSelected);

        private void PickChoice(int index)
        {
            if (index < 0 || index >= _choiceTexts.Count || _onChoose == null || IsLogOpen) return;
            _dialogueLog.Add((null, "▶ " + _choiceTexts[index], Cyan));
            Action<int> choose = _onChoose;
            choose(index);
        }

        /// <summary>건너뛴 대사도 로그에는 남긴다</summary>
        public void LogLine(string speaker, string text, Color tag) => _dialogueLog.Add((speaker, text ?? "", tag));

        public void HideDialogue()
        {
            _dialogue.SetActive(false);
            _fullLine = null;
            _choiceTexts.Clear();
            _onChoose = null;
            if (IsLogOpen) ClosePopups();
            _logOpen = false;
        }

        private void ToggleAuto()
        {
            _autoPlay = !_autoPlay;
            _autoWait = 0;
            Transform t = _dialogueButtons != null ? _dialogueButtons.Find("Button:자동") : null;
            Text label = t != null ? t.GetComponentInChildren<Text>() : null;
            if (label != null) label.color = _autoPlay ? Cyan : Muted;
        }

        /// <summary>마우스가 로그·자동·건너뛰기나 선택지 위에 있으면 true (그 클릭은 "다음 대사"로 치지 않는다)</summary>
        public bool PointerOnDialogueControls(Vector2 screen)
        {
            if (PointerOnNarrationSkip(screen)) return true;
            if (!_dialogue.activeSelf) return false;
            if (_dialogueButtons != null && RectTransformUtility.RectangleContainsScreenPoint(_dialogueButtons, screen, null)) return true;
            foreach (Transform c in _choices)
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)c, screen, null)) return true;
            return false;
        }

        // ---------------- 로그 ----------------
        public void ToggleDialogueLog()
        {
            if (IsLogOpen) { ClosePopups(); return; }
            ClosePopups();
            _logOpen = true;
            const float width = 800, height = 520;
            WindowFrame(_popupRoot, "dialogue.log — 지난 대사", null, (1280 - width) / 2, 70, width, height, true,
                () => ClosePopups(), out RectTransform body, out GameObject blocker);
            Transform bar = blocker.transform.Find("Window/TitleBar");
            for (int i = 0; i < 2 && bar != null; i++) bar.Find("WindowButton" + i)?.gameObject.SetActive(false);

            RectTransform viewport = Node(body, "Viewport");
            Inset(viewport, 24 * S, 18 * S, 24 * S, 52 * S);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = Fill(viewport, "Hit", new Color(0, 0, 0, 0), raycast: true);
            Inset(hit, 0, 0, 0, 0);
            RectTransform content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14 * S;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;

            if (_dialogueLog.Count == 0)
            {
                Text empty = Label(content, "아직 대사가 없어요.", 12 * S, Muted, TextAnchor.MiddleLeft);
                empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            foreach (var (speaker, text, tag) in _dialogueLog)
            {
                string head = speaker == null ? ""
                    : $"<b><color=#{ColorUtility.ToHtmlStringRGB(tag)}>{speaker}</color></b>\n";
                Text entry = Label(content, head + (speaker == null ? $"<color=#22d3ee>{text}</color>" : text), 12 * S * 1.25f, Fg,
                    TextAnchor.UpperLeft);
                entry.horizontalOverflow = HorizontalWrapMode.Wrap;
                entry.lineSpacing = 1.3f;
                entry.raycastTarget = false;
            }
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0;

            Text hint = Label(body, "휠로 넘겨 보기 · 닫기 <color=#eaf2ff>[Esc]</color>", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(hint, 24 * S, height * S - WinBorder * 2 - WinTitleH - 40 * S, 300 * S, 28 * S);
            GhostButton(body, "닫기", 12 * S, () => ClosePopups(), out RectTransform close);
            AtRight(close, 24 * S, height * S - WinBorder * 2 - WinTitleH - 44 * S, 100 * S, 32 * S);
        }

        // ---------------- 매 프레임 ----------------
        private void TickDialogue()
        {
            if (!_dialogue.activeSelf || _fullLine == null) return;
            if ((int)_typed < _fullLine.Length)
            {
                _typed += Time.unscaledDeltaTime * 40f; // 초당 40자
                _line.text = RichPrefix(_fullLine, Mathf.Min((int)_typed, _fullLine.Length));
                return;
            }
            _line.text = _fullLine;
            bool waiting = _choiceTexts.Count == 0;
            if (_nextArrow != null) _nextArrow.SetActive(waiting);
            if (!_autoPlay || !waiting || HasPopup) return;
            _autoWait += Time.unscaledDeltaTime;
            if (_autoWait >= 1.2f + _fullLine.Length * 0.04f)
            {
                _autoWait = 0;
                OnDialogueNext?.Invoke();
            }
        }

        /// <summary>타자 효과 중 앞 n글자. 리치 텍스트 태그는 글자로 세지 않고 통째로 넣는다</summary>
        private static string RichPrefix(string text, int n)
        {
            if (text.IndexOf('<') < 0) return text.Substring(0, n);
            var sb = new System.Text.StringBuilder();
            int shown = 0;
            for (int i = 0; i < text.Length && shown < n; i++)
            {
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i);
                    if (close > i) { sb.Append(text, i, close - i + 1); i = close; continue; }
                }
                sb.Append(text[i]);
                shown++;
            }
            return sb.ToString();
        }
    }
}
