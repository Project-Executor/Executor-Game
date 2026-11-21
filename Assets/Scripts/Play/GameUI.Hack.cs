using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 감염된 컴퓨터 타자 해킹 창 (시안 Hack / HackSuccess / HackFail, hack.exe).
    /// 진행 중: 안내 줄(★ 목표) · 남은 시간 막대 · 단어 칩(맞힌 것 ✓, 지금 단어 하늘색) · 큰 지금 단어(친 만큼 초록) · 입력칸.
    /// 결과: 성공(복구 완료, 걸린 시간, ★ 달성, 작동한 장치) / 실패(해킹 실패, 닫기·다시 도전)
    /// </summary>
    public partial class GameUI
    {
        private const float HackW = 600;
        private GameObject _hack;
        private RectTransform _hackChips, _hackBarFill;
        private Text _hackInfo, _hackGoal, _hackTimer, _hackWord;
        private InputField _hackInput;
        private FieldFrame _hackFrame;
        private RectTransform _hackPlay;
        private float _hackWrongUntil;

        /// <summary>해킹 창 입력값</summary>
        public string HackInput => _hackInput != null ? _hackInput.text : "";

        /// <summary>해킹 입력칸에 커서가 있는지</summary>
        public bool HackInputFocused => _hackInput != null && _hackInput.isFocused;

        public bool IsHackShown => _hack != null && _hack.activeSelf;

        private void BuildHack()
        {
            var dim = Fill(_root, "Hack", Hex(0x0a0716, 0.55f), raycast: true);
            Inset(dim, 0, 0, 0, 0);
            _hack = dim.gameObject;
        }

        /// <param name="title">창 제목 뒤 이름 (예: 감염된 PC)</param>
        /// <param name="info">위 안내 줄 (예: 감염된 PC · 단어 5개를 모두 입력하면 복구)</param>
        /// <param name="goal">오른쪽 ★ 목표 (없으면 null)</param>
        public void ShowHack(string title, string info, string goal)
        {
            UiKit.Clear(_hack.transform);
            _hack.SetActive(true);
            RectTransform body = HackWindow($"hack.exe — {title}", Cyan, WindowLine, TitleBar, 150, 410, out _);
            float w = HackW * S - WinBorder * 2 - 48 * S;
            float y = 20 * S;
            _hackInfo = Label(body, info, 12 * S, Muted, TextAnchor.MiddleLeft);
            At(_hackInfo, 24 * S, y, w, 16 * S);
            _hackGoal = Label(body, goal ?? "", 12 * S, Yellow, TextAnchor.MiddleRight);
            At(_hackGoal, 24 * S, y, w, 16 * S);
            y += 16 * S + 18 * S;
            TimeBar(body, ref y, w, out _hackBarFill, out _hackTimer);
            y += 18 * S;
            _hackChips = Node(body, "Chips");
            At(_hackChips, 24 * S, y, w, 24 * S);
            y += 24 * S + 18 * S;
            Image wordBox = Fill(body, "Word", Night);
            At(wordBox, 24 * S, y, w, 92 * S);
            _hackWord = Label(wordBox.transform, "", 48 * S, Fg, TextAnchor.MiddleCenter, UiKit.Mono);
            Inset(_hackWord, 0, 0, 0, 0);
            y += 92 * S + 18 * S;
            Text inputLabel = Label(body, "입력", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(inputLabel, 24 * S, y, w, 16 * S);
            y += 16 * S + 6 * S;
            _hackInput = UiKit.Input(body, "", limit: 50, size: (int)(24 * S));
            At(_hackInput, 24 * S, y, w, 44 * S);
            _hackFrame = FieldFrame.Apply(_hackInput);
            _hackInput.textComponent.rectTransform.offsetMin = new Vector2(12 * S, 0);
            y += 44 * S + 18 * S;
            var (enter, enterW) = KeyHint(body, "Enter", 24 * S, y);
            Text help = Label(body, "확인 · 틀리면 입력이 지워지고 같은 단어를 다시", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(help, 24 * S + enterW + 6 * S, y, w, 16 * S);
            Text giveUp = Label(body, "포기", 12 * S, Muted, TextAnchor.MiddleRight);
            At(giveUp, 24 * S, y, w, 16 * S);
            var (esc, escW) = KeyHint(body, "Esc", 0, y);
            esc.anchoredPosition = new Vector2(24 * S + w - giveUp.preferredWidth - 6 * S - escW, -y);
            _hackPlay = body;
            ClearHackInput();
        }

        /// <summary>입력칸을 비우고 다시 입력할 수 있게 한다</summary>
        public void ClearHackInput()
        {
            if (_hackInput == null) return;
            _hackInput.text = "";
            FocusHackInput();
        }

        /// <summary>해킹 입력칸에 포커스를 준다 (Enter로 포커스가 빠졌을 때 다음 프레임에 다시 잡는 용도)</summary>
        public void FocusHackInput()
        {
            if (_hackInput == null || !_hackInput.gameObject.activeInHierarchy) return;
            _hackInput.Select();
            _hackInput.ActivateInputField();
        }

        /// <summary>틀린 입력: 잠깐 입력칸 테두리를 빨갛게</summary>
        public void FlashHackWrong() => _hackWrongUntil = Time.unscaledTime + 0.5f;

        /// <summary>진행 화면 갱신: 단어 칩, 지금 단어(친 만큼 초록), 남은 시간</summary>
        public void UpdateHack(IReadOnlyList<string> words, int index, float timeLeft, float timeLimit)
        {
            if (_hackPlay == null) return;
            DrawChips(_hackChips, words, index, true);
            string current = index < words.Count ? words[index] : "";
            string typed = _hackInput.text;
            int match = 0;
            while (match < typed.Length && match < current.Length && typed[match] == current[match]) match++;
            bool wrongPrefix = typed.Length > match;
            _hackWord.text = $"<color={(wrongPrefix ? "#ff4d5e" : "#5cf08a")}>{Spaced(current.Substring(0, match))}</color>" +
                             $"<color=#4b5675>{Spaced(current.Substring(match))}</color>";
            SetTimer(_hackBarFill, _hackTimer, timeLeft, timeLimit, false);
            _hackFrame.State = Time.unscaledTime < _hackWrongUntil || wrongPrefix ? FieldFrame.Mark.Bad : FieldFrame.Mark.None;
        }

        /// <summary>
        /// 결과 창. success면 "복구 완료"(걸린 시간, ★ 줄, [EXEC] 장치 줄, 계속하기),
        /// 아니면 "해킹 실패"(TimeoutException, 남은 0초, 닫기 Esc / 다시 도전 E)
        /// </summary>
        public void ShowHackResult(bool success, float elapsed, float timeLimit, IReadOnlyList<string> words, int index,
            string starLine, bool starMet, IReadOnlyList<string> effects, Action onPrimary, Action onClose)
        {
            UiKit.Clear(_hack.transform);
            _hack.SetActive(true);
            _hackPlay = null;
            _hackInput = null;
            float w = HackW * S - WinBorder * 2 - 48 * S;
            // 높이 계산 (위에서부터)
            float h = 24 * S + 16 * S + 6 * S + 52 * S + 18 * S + (success ? 0 : 28 * S + 18 * S) + 24 * S + 18 * S
                      + (starLine != null ? 36 * S + 18 * S : 0) + (effects.Count > 0 ? 24 * S + effects.Count * 22 * S - 6 * S + 18 * S : 0)
                      + (success ? 0 : 40 * S + 18 * S) + 44 * S + 24 * S;
            float heightPx = WinBorder * 2 + WinTitleH + h;
            RectTransform body = success
                ? HackWindow("hack.exe — 복구 완료", Green, Hex(0x1f9e55), TitleBar, 170, heightPx / S, out _)
                : HackWindow("hack.exe — 시간 초과", Red, Hex(0xa3203a), Hex(0x4a1a2c), 170, heightPx / S, out _);
            float y = 24 * S;
            Text code = Label(body, success ? "system restored · exit code 0" : $"TimeoutException · {timeLimit:0.0}s", 12 * S,
                success ? Green : Red, TextAnchor.MiddleLeft, UiKit.Mono);
            At(code, 24 * S, y, w, 16 * S);
            Text head = Label(body, success ? "복구 완료" : "해킹 실패", 48 * S, success ? Green : Red, TextAnchor.MiddleLeft, bold: true);
            At(head, 24 * S, y + 16 * S + 6 * S, w, 52 * S);
            if (success)
            {
                Text tl = Label(body, "걸린 시간", 12 * S, Muted, TextAnchor.MiddleRight);
                At(tl, 24 * S, y + 16 * S + 6 * S + 4 * S, w, 16 * S);
                Text tv = Label(body, $"{elapsed:0.0}s", 36 * S, Fg, TextAnchor.MiddleRight, UiKit.Mono);
                At(tv, 24 * S, y + 16 * S + 6 * S + 16 * S, w, 40 * S);
            }
            y += 16 * S + 6 * S + 52 * S + 18 * S;
            if (!success)
            {
                TimeBar(body, ref y, w, out RectTransform fill, out Text timer);
                SetTimer(fill, timer, 0, timeLimit, true);
                y += 18 * S;
            }
            RectTransform chips = Node(body, "Chips");
            At(chips, 24 * S, y, w, 24 * S);
            DrawChips(chips, words, index, false);
            y += 24 * S + 18 * S;
            if (starLine != null)
            {
                Image star = Fill(body, "Star", starMet ? Hex(0xffd84a, 0.10f) : Hex(0xff4d5e, 0.08f));
                At(star, 24 * S, y, w, 36 * S);
                Border(star.transform, starMet ? YellowDark : Hex(0xa3203a), 1 * S);
                Text st = Label(star.transform, starLine, 12 * S, Fg, TextAnchor.MiddleLeft);
                Inset(st, 12 * S, 0, 12 * S, 0);
                y += 36 * S + 18 * S;
            }
            if (effects.Count > 0)
            {
                float eh = 24 * S + effects.Count * 22 * S - 6 * S;
                Image box = Fill(body, "Effects", Night);
                At(box, 24 * S, y, w, eh);
                for (int i = 0; i < effects.Count; i++)
                {
                    Text e = Label(box.transform, effects[i], 12 * S, Fg, TextAnchor.MiddleLeft, UiKit.Mono);
                    At(e, 12 * S, 12 * S + i * 22 * S, w - 24 * S, 16 * S);
                }
                y += eh + 18 * S;
            }
            if (!success)
            {
                Text note = Label(body, "컴퓨터는 아직 감염된 상태예요. 다시 도전하면 단어를 새로 뽑고, <color=#eaf2ff>시작할 때 1턴</color>을 써요.",
                    12 * S, Muted, TextAnchor.UpperLeft);
                note.horizontalOverflow = HorizontalWrapMode.Wrap;
                note.lineSpacing = 1.3f;
                At(note, 24 * S, y, w, 40 * S);
                y += 40 * S + 18 * S;
                float half = (w - 10 * S) / 2;
                GhostButton(body, "닫기  <color=#eaf2ff>[Esc]</color>", 12 * S, onClose, out RectTransform close);
                At(close, 24 * S, y, half, 44 * S);
                PrimaryButton(body, "다시 도전  [E]", 12 * S, onPrimary, out RectTransform retry);
                At(retry, 24 * S + half + 10 * S, y, half, 44 * S);
            }
            else
            {
                PrimaryButton(body, "계속하기", 12 * S, onPrimary, out RectTransform cont);
                At(cont, 24 * S, y, w, 44 * S);
            }
        }

        public void HideHack()
        {
            if (_hackInput != null) _hackInput.DeactivateInputField();
            _hackInput = null;
            _hackPlay = null;
            if (_hack != null)
            {
                UiKit.Clear(_hack.transform);
                _hack.SetActive(false);
            }
        }

        /// <summary>hack.exe 창 (색 테두리·제목줄 아래 줄 색을 고를 수 있다). 시안 left 340, 너비 600</summary>
        private RectTransform HackWindow(string title, Color line, Color border, Color bar, float top, float height, out RectTransform win)
        {
            win = Frame(_hack.transform, "Window", Ink, border, WinBorder, 6 * S);
            At(win, 340 * S, top * S, HackW * S, height * S);
            Image titleBar = Fill(win, "TitleBar", bar);
            At(titleBar, WinBorder, WinBorder, HackW * S - WinBorder * 2, 32 * S);
            Image titleLine = Fill(win, "TitleLine", line);
            At(titleLine, WinBorder, WinBorder + 32 * S, HackW * S - WinBorder * 2, 2 * S);
            Text t = Label(titleBar.transform, title, 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            Inset(t, 12 * S, 0, 40 * S, 0);
            Image close = Fill(titleBar.transform, "Close", Hex(0xa3203a));
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(1, 0.5f);
            close.rectTransform.pivot = new Vector2(1, 0.5f);
            close.rectTransform.anchoredPosition = new Vector2(-8 * S, 0);
            close.rectTransform.sizeDelta = new Vector2(16 * S, 16 * S);
            Border(close.transform, Red, 2 * S);
            RectTransform body = Node(win, "Body");
            Inset(body, WinBorder, WinBorder + WinTitleH, WinBorder, WinBorder);
            BorderOnTop(win);
            return body;
        }

        private static void TimeBar(RectTransform body, ref float y, float w, out RectTransform fill, out Text timer)
        {
            Image track = Fill(body, "TimeTrack", Track);
            At(track, 24 * S, y + 9 * S, w - 84 * S - 12 * S, 10 * S);
            Border(track.transform, TrackLine, 1 * S);
            Image f = Fill(track.transform, "Fill", Cyan);
            fill = f.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(1, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            timer = Label(body, "", 24 * S, Cyan, TextAnchor.MiddleRight, UiKit.Mono);
            At(timer, 24 * S + w - 84 * S, y, 84 * S, 28 * S);
            y += 28 * S;
        }

        private static void SetTimer(RectTransform fill, Text timer, float timeLeft, float timeLimit, bool failed)
        {
            float t = timeLimit <= 0 ? 0 : Mathf.Clamp01(timeLeft / timeLimit);
            fill.anchorMax = new Vector2(t, 1);
            Color c = failed || t <= 0.2f ? Red : Cyan;
            fill.GetComponent<Image>().color = c;
            timer.color = c;
            timer.text = $"{Mathf.Max(0, timeLeft):00.0}s";
        }

        /// <summary>단어 칩: 맞힌 것 ✓ 초록, 지금 단어(진행 중일 때) 하늘색 채움, 남은 것 흐리게</summary>
        private static void DrawChips(RectTransform row, IReadOnlyList<string> words, int index, bool live)
        {
            UiKit.Clear(row);
            float x = 0;
            for (int i = 0; i < words.Count; i++)
            {
                bool done = i < index, current = live && i == index;
                Text t = Label(row, words[i], 12 * S, done ? Green : current ? Ink : Dim, TextAnchor.MiddleCenter, UiKit.Mono);
                float tw = t.preferredWidth + 20 * S + (done ? 14 * S : 0);
                Image chip = Fill(row, "Chip", done ? Hex(0x5cf08a, 0.10f) : current ? Cyan : Night);
                At(chip, x, 0, tw, 24 * S);
                if (done)
                {
                    Border(chip.transform, Hex(0x1f9e55), 1 * S);
                    Image check = Icon(chip.transform, "desktop/check_white", 10 * S);
                    if (check != null)
                    {
                        check.color = Green;
                        At(check, 8 * S, 8 * S, 9 * S, 8 * S);
                    }
                }
                t.transform.SetParent(chip.transform, false);
                Inset(t, done ? 14 * S : 0, 0, 0, 0);
                x += tw + 6 * S;
            }
        }

        /// <summary>지금 단어 글자 (고정폭이라 따로 띄우지 않는다)</summary>
        private static string Spaced(string s) => s;

        private static (RectTransform box, float width) KeyHint(RectTransform body, string key, float left, float top)
        {
            Kbd(body, key, out RectTransform box);
            float kw = box.GetComponentInChildren<Text>().preferredWidth + 12 * S;
            At(box, left, top - 1 * S, kw, 18 * S);
            return (box, kw);
        }
    }
}
