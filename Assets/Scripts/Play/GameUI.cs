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
    /// 여기에는 대화창 1760x300·이름표·선택지·스탠딩 일러스트, 나레이션, 결과, 해킹 창, 팝업이 있다.
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

        public void ClosePopups() => Clear(_popupRoot);

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
        private Button _resultRank;

        /// <summary>결과 화면 오른쪽 위 순위 버튼. label이 null이면 숨긴다</summary>
        public void SetResultRank(string label, Action onClick)
        {
            _resultRank.gameObject.SetActive(label != null);
            if (label == null) return;
            _resultRank.GetComponentInChildren<Text>().text = label;
            _resultRank.onClick.RemoveAllListeners();
            _resultRank.onClick.AddListener(() => onClick?.Invoke());
        }

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

        public void ShowResult(string text, bool success, IEnumerable<(string label, Action onClick)> buttons)
        {
            _result.SetActive(true);
            _resultRank.gameObject.SetActive(false);
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

        /// <summary>대화 중 게임 화면을 덮는 검정 판의 불투명도 (0 투명 ~ 1 검정)</summary>
        private const float DialogueDim = 0.55f;

        private void BuildDialogue()
        {
            _dialogue = new GameObject("Dialogue", typeof(RectTransform));
            _dialogue.transform.SetParent(_root, false);
            Stretch((RectTransform)_dialogue.transform);

            // 대화 중에는 뒤의 게임 화면을 어둡게 (일러스트·대화창보다 뒤, 클릭은 그대로 통과)
            var dim = Panel(_dialogue.transform, "Dim", new Color(0, 0, 0, DialogueDim));
            Stretch(dim);
            dim.GetComponent<Image>().raycastTarget = false;

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

        // ---------------- 타자 해킹 (감염된 컴퓨터) ----------------
        private GameObject _hack;
        private Text _hackTitle, _hackTimer, _hackWords, _hackStatus;
        private Image _hackBar;
        private InputField _hackInput;

        /// <summary>해킹 창 입력값</summary>
        public string HackInput => _hackInput.text;

        public void ShowHack(string title)
        {
            _hack.SetActive(true);
            _hackTitle.text = title;
            _hackStatus.text = "";
            ClearHackInput();
        }

        /// <summary>입력칸을 비우고 다시 입력할 수 있게 한다</summary>
        public void ClearHackInput()
        {
            _hackInput.text = "";
            FocusHackInput();
        }

        /// <summary>해킹 입력칸에 커서가 있는지</summary>
        public bool HackInputFocused => _hackInput.isFocused;

        /// <summary>해킹 입력칸에 포커스를 준다 (Enter로 포커스가 빠졌을 때 다음 프레임에 다시 잡는 용도)</summary>
        public void FocusHackInput()
        {
            _hackInput.Select();
            _hackInput.ActivateInputField();
        }

        /// <param name="words">단어 줄 (리치 텍스트)</param>
        public void UpdateHack(string words, float timeLeft, float timeLimit, string status, bool statusGood)
        {
            _hackWords.text = words;
            _hackTimer.text = $"{Mathf.CeilToInt(timeLeft)}초";
            float t = timeLimit <= 0 ? 0 : Mathf.Clamp01(timeLeft / timeLimit);
            _hackBar.rectTransform.anchorMax = new Vector2(t, 1);
            _hackBar.color = t > 0.5f ? Good : t > 0.2f ? Warn : Bad;
            _hackTimer.color = t > 0.2f ? TextColor : Bad;
            if (status != null)
            {
                _hackStatus.text = status;
                _hackStatus.color = statusGood ? Good : Bad;
            }
        }

        public void HideHack()
        {
            _hackInput.DeactivateInputField();
            _hack.SetActive(false);
        }

        private void BuildHack()
        {
            var dim = Panel(_root, "Hack", new Color(0, 0, 0, 0.6f));
            Stretch(dim);
            _hack = dim.gameObject;
            var panel = Panel(dim, "HackPanel", PanelColor, Bad);
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 560));
            _hackTitle = Label(panel, "", 34, TextAnchor.MiddleLeft, Bad, bold: true);
            Place(_hackTitle, new Vector2(0, 1), new Vector2(40, -30), new Vector2(800, 50), new Vector2(0, 1));
            _hackTimer = Label(panel, "", 34, TextAnchor.MiddleRight, bold: true);
            Place(_hackTimer, new Vector2(1, 1), new Vector2(-40, -30), new Vector2(200, 50), new Vector2(1, 1));
            var barBack = Panel(panel, "TimeBar", new Color32(0x22, 0x26, 0x2e, 0xff));
            Place(barBack, new Vector2(0.5f, 1), new Vector2(0, -95), new Vector2(1020, 12), new Vector2(0.5f, 1));
            _hackBar = Panel(barBack, "Fill", Good).GetComponent<Image>();
            Stretch(_hackBar.rectTransform);
            _hackBar.rectTransform.pivot = new Vector2(0, 0.5f);
            _hackWords = Label(panel, "", 34, TextAnchor.MiddleCenter);
            _hackWords.supportRichText = true;
            _hackWords.lineSpacing = 1.4f;
            Place(_hackWords, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1020, 170), new Vector2(0.5f, 1));
            _hackInput = Input(panel, "보이는 단어를 입력하고 Enter", limit: 50, size: 36);
            _hackInput.textComponent.alignment = TextAnchor.MiddleCenter;
            ((Text)_hackInput.placeholder).alignment = TextAnchor.MiddleCenter;
            Place(_hackInput, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(700, 80), new Vector2(0.5f, 0));
            _hackStatus = Label(panel, "", 24, TextAnchor.MiddleCenter, Good);
            Place(_hackStatus, new Vector2(0.5f, 0), new Vector2(0, 95), new Vector2(1000, 40), new Vector2(0.5f, 0));
            var hint = Label(panel, "Enter: 입력 · Esc: 포기 (다시 도전할 때마다 1턴)", 22, TextAnchor.MiddleCenter, Muted);
            Place(hint, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1000, 36), new Vector2(0.5f, 0));
        }

        private void BuildResult()
        {
            var panel = Panel(_root, "Result", PanelColor, Line);
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 460));
            _result = panel.gameObject;
            _resultText = Label(panel, "", 36, TextAnchor.MiddleCenter, bold: true);
            Place(_resultText, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(820, 260), new Vector2(0.5f, 1));
            _resultRank = Button(panel, "순위", null, 22);
            Place(_resultRank, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(220, 56), new Vector2(1, 1));
            _resultRank.gameObject.SetActive(false);
            _resultButtons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            _resultButtons.SetParent(panel, false);
            Place(_resultButtons, new Vector2(0.5f, 0), new Vector2(-395, 60), new Vector2(790, 72), new Vector2(0, 0));
        }
    }
}
