using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.UiKit;

namespace Executor.Play
{
    /// <summary>
    /// 게임 화면 UI (와이어프레임 stage(ex) 배치, 기준 1920x1080).
    /// <list type="bullet">
    ///   <item>왼쪽 위: 미니맵(누르면 전체 맵) + 오른쪽 아이템 칸 / 그 아래: 퀘스트(별 조건 3줄, 누르면 자세히). 왼쪽 아래는 비워 둔다</item>
    ///   <item>오른쪽 위 줄: 남은 턴 · 수집 · 채팅 · 설정</item>
    ///   <item>가운데: 게임 화면 (BoardView가 GameArea 안에 맞춰 그린다)</item>
    ///   <item>대화창 1760x300, 이름표 280x60, 선택지 600x64, 스탠딩 일러스트 높이 1000</item>
    /// </list>
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        /// <summary>게임 화면 영역 (화면 비율 0~1, 왼쪽 아래 기준). 왼쪽 패널과 위쪽 버튼을 피한다</summary>
        public static readonly Rect GameArea = new Rect(552f / 1920, 64f / 1080, 1320f / 1920, 872f / 1080);

        private RectTransform _root;
        private RectTransform _popupRoot;

        // HUD
        private GameObject _hud;
        private Text _stageTitle, _turns, _keys;
        private Image _turnsBox;
        private RawImage _minimap;
        private readonly Text[] _questRows = new Text[3];
        /// <summary>퀘스트 팝업에 보여 줄 자세한 내용 (목표·별 조건·체크포인트·안내)</summary>
        private string _questDetail = "";
        private GameObject _keyBox;
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
        private Button _nameCheck;
        private Text _nameStatus;

        /// <summary>닉네임 입력의 중복 확인 버튼</summary>
        public Action OnCheckNickname;

        // 결과
        private GameObject _result;
        private Text _resultText;
        private RectTransform _resultButtons;

        public Action OnChat, OnSettings, OnCollect;

        /// <summary>미니맵을 눌렀을 때 (전체 맵 팝업), 퀘스트를 눌렀을 때 (자세히)</summary>
        public Action OnMinimap, OnQuest;

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
            SetQuestStars(new (string, QuestState)[0]);
            SetButtonText(_collect, $"수집 {collected}/{collectTotal}");
        }

        /// <summary>
        /// 지금 구간의 남은 턴. 0이면 주의 색, 넘기면 -1, -2처럼 빨간색으로 (실패는 아니고 도착이 불안정이 된다).
        /// 제한이 없으면 ∞
        /// </summary>
        public void SetTurns(int? remaining, int keys)
        {
            _turns.text = remaining?.ToString() ?? "∞";
            _turns.color = remaining < 0 ? Bad : remaining == 0 ? Warn : TextColor;
            _turnsBox.color = remaining < 0 ? (Color)new Color32(0x3a, 0x14, 0x1c, 0xee) : PanelColor;
            // 열쇠는 남은 턴 옆 작은 상자에 그림과 개수로 (가지고 있을 때만)
            _keyBox.SetActive(keys > 0);
            _keys.text = "×" + keys;
        }

        /// <summary>퀘스트 팝업에 보여 줄 자세한 내용</summary>
        public void SetQuest(string text) => _questDetail = text;

        public string QuestDetail => _questDetail;

        public enum QuestState { Pending, Done, Warn }

        /// <summary>퀘스트 칸의 별 조건 줄 (최대 3줄). 달성하면 초록 ✓, 지금 못 미치면 주황</summary>
        public void SetQuestStars(IReadOnlyList<(string text, QuestState state)> rows)
        {
            for (int i = 0; i < _questRows.Length; i++)
            {
                bool has = i < rows.Count;
                _questRows[i].gameObject.SetActive(has);
                if (!has) continue;
                var (text, state) = rows[i];
                _questRows[i].text = (state == QuestState.Done ? "✓ " : "· ") + text;
                _questRows[i].color = state == QuestState.Done ? Good : state == QuestState.Warn ? Warn : TextColor;
            }
        }

        /// <summary>미니맵 그림을 바꾼다. 칸 비율을 지키며 미니맵 상자 안에 맞춘다</summary>
        public void SetMinimap(Texture2D texture)
        {
            _minimap.texture = texture;
            if (texture == null) return;
            var inner = new Vector2(MinimapSize.x - 24, MinimapSize.y - 52);
            float scale = Mathf.Min(inner.x / texture.width, inner.y / texture.height);
            _minimap.rectTransform.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
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
            _nameCheck.gameObject.SetActive(askName);
            _nameStatus.gameObject.SetActive(askName);
            _nameStatus.text = "";
            _narrationHint.text = askName ? "닉네임을 입력하고 Enter (2~20자, 다른 사람과 겹칠 수 없습니다)" : "E / Space / 클릭: 다음";
            if (askName)
            {
                _nameInput.text = currentName ?? "";
                _nameInput.Select();
                _nameInput.ActivateInputField();
            }
        }

        public void SetNarrationHint(string text) => _narrationHint.text = text;

        /// <summary>닉네임 입력 아래 상태 문구 (사용 가능 / 이미 사용 중 / 오류)</summary>
        public void SetNicknameStatus(string text, bool ok)
        {
            _nameStatus.text = text;
            _nameStatus.color = ok ? Good : Bad;
        }

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
        // 배치 (기준 1920x1080). 게임 화면(GameArea)은 x 552~1872, 위에서 144 아래부터.
        // 왼쪽 위: 미니맵 + 오른쪽 아이템 칸, 그 아래 퀘스트. 왼쪽 아래는 비워 둔다 (모바일 방향 패드 자리).
        // 오른쪽 위 줄: 남은 턴 · 수집 · 채팅 · 설정 (엄지가 가리지 않는 위쪽에 계속 볼 값을 둔다)
        private const float Left = 48;
        private static readonly Vector2 MinimapSize = new Vector2(360, 300);
        private const float ItemSlot = 84;

        private void BuildHud()
        {
            _hud = new GameObject("Hud", typeof(RectTransform));
            _hud.transform.SetParent(_root, false);
            Stretch((RectTransform)_hud.transform);

            // 미니맵 (왼쪽 위). 누르면 전체 맵 팝업
            var mapButton = Button(_hud.transform, "", () => OnMinimap?.Invoke(), 20, PanelColor);
            Place(mapButton, new Vector2(0, 1), new Vector2(Left, -48), MinimapSize, new Vector2(0, 1));
            var mapLabel = Label(mapButton.transform, "미니맵 · 누르면 전체 맵", 18, TextAnchor.UpperLeft, Muted);
            Stretch(mapLabel.rectTransform, 12);
            _minimap = new GameObject("Minimap", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _minimap.transform.SetParent(mapButton.transform, false);
            _minimap.raycastTarget = false;
            Place(_minimap, new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(MinimapSize.x - 24, MinimapSize.y - 52), new Vector2(0.5f, 0));

            // 아이템 칸 (미니맵 오른쪽 세로 줄). 지금은 열쇠 하나, 가지고 있을 때만 보인다. 아이템이 늘면 아래로 쌓는다
            float itemX = Left + MinimapSize.x + 12;
            var keyBox = Panel(_hud.transform, "Keys", PanelColor, Line);
            Place(keyBox, new Vector2(0, 1), new Vector2(itemX, -48), new Vector2(ItemSlot, ItemSlot), new Vector2(0, 1));
            _keyBox = keyBox.gameObject;
            var keyIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            keyIcon.transform.SetParent(keyBox, false);
            keyIcon.sprite = ArtCatalog.ForObject(Data.MapObjectTypes.Key);
            keyIcon.preserveAspect = true;
            keyIcon.raycastTarget = false;
            Place(keyIcon, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(48, 48), new Vector2(0.5f, 1));
            _keys = Label(keyBox, "", 20, TextAnchor.LowerCenter, new Color32(0xff, 0xd2, 0x3f, 0xff), bold: true);
            Stretch(_keys.rectTransform, 4);

            // 퀘스트 (미니맵 아래): 별 조건 3줄. 누르면 자세히(목표·체크포인트·안내)
            float questY = -48 - MinimapSize.y - 16;
            float questW = MinimapSize.x + 12 + ItemSlot;
            var quest = Button(_hud.transform, "", () => OnQuest?.Invoke(), 20, PanelColor);
            Place(quest, new Vector2(0, 1), new Vector2(Left, questY), new Vector2(questW, 184), new Vector2(0, 1));
            var qTitle = Label(quest.transform, "퀘스트", 22, TextAnchor.UpperLeft, bold: true);
            Stretch(qTitle.rectTransform, 14);
            var qMore = Label(quest.transform, "자세히 ›", 18, TextAnchor.UpperRight, Muted);
            Stretch(qMore.rectTransform, 16);
            for (int i = 0; i < _questRows.Length; i++)
            {
                _questRows[i] = Label(quest.transform, "", 20, TextAnchor.UpperLeft);
                Place(_questRows[i], new Vector2(0, 1), new Vector2(14, -50 - i * 42), new Vector2(questW - 28, 40), new Vector2(0, 1));
            }

            // 남은 턴 (오른쪽 위 줄, 수집 버튼 왼쪽)
            var box = Panel(_hud.transform, "Turns", PanelColor, Line);
            Place(box, new Vector2(1, 1), new Vector2(-500, -40), new Vector2(240, 84), new Vector2(1, 1));
            _turnsBox = box.GetComponent<Image>();
            var label = Label(box, "남은 턴", 20, TextAnchor.MiddleLeft, Muted);
            Stretch(label.rectTransform, 16);
            _turns = Label(box, "", 48, TextAnchor.MiddleRight, bold: true);
            Stretch(_turns.rectTransform, 16);

            // 스테이지 이름 (위쪽, 게임 화면 왼쪽 위 ~ 남은 턴 사이 가운데)
            _stageTitle = Label(_hud.transform, "", 28, TextAnchor.MiddleCenter, Muted, bold: true);
            Place(_stageTitle, new Vector2(0.5f, 1), new Vector2(-94, -58), new Vector2(560, 40));

            // 수집 / 채팅 / 설정 (오른쪽 위)
            var settings = Button(_hud.transform, "설정", () => OnSettings?.Invoke(), 22);
            Place(settings, new Vector2(1, 1), new Vector2(-48, -48), new Vector2(110, 60), new Vector2(1, 1));
            var chat = Button(_hud.transform, "채팅", () => OnChat?.Invoke(), 22);
            Place(chat, new Vector2(1, 1), new Vector2(-182, -48), new Vector2(110, 60), new Vector2(1, 1));
            _collect = Button(_hud.transform, "수집 0/0", () => OnCollect?.Invoke(), 22);
            Place(_collect, new Vector2(1, 1), new Vector2(-316, -48), new Vector2(170, 60), new Vector2(1, 1));

            var help = Label(_hud.transform, "W A S D 이동 · E 대화 · Q 워프 · M 전체 맵 · R 다시 · Esc 메뉴", 22, TextAnchor.MiddleLeft, Muted);
            Place(help, new Vector2(0, 0), new Vector2(48, 20), new Vector2(900, 36), new Vector2(0, 0));
        }

        private void BuildToast()
        {
            var panel = Panel(_root, "Toast", PanelColor, Line);
            Place(panel, new Vector2(0.5f, 1), new Vector2(252, -150), new Vector2(1000, 100));
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
            // 닉네임 입력 + 중복 확인 버튼 (한 줄)
            _nameInput = Input(panel, "닉네임 (2~20자)", limit: 20, size: 36);
            _nameInput.textComponent.alignment = TextAnchor.MiddleCenter;
            ((Text)_nameInput.placeholder).alignment = TextAnchor.MiddleCenter;
            Place(_nameInput, new Vector2(0.5f, 0.5f), new Vector2(-110, -160), new Vector2(560, 80));
            _nameCheck = Button(panel, "중복 확인", () => OnCheckNickname?.Invoke(), 28);
            Place(_nameCheck, new Vector2(0.5f, 0.5f), new Vector2(290, -160), new Vector2(200, 80));
            _nameStatus = Label(panel, "", 26, TextAnchor.MiddleCenter, Good);
            Place(_nameStatus, new Vector2(0.5f, 0.5f), new Vector2(0, -235), new Vector2(800, 40));
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
