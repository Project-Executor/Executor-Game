using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 시작(잠금) 화면과 로그인·회원가입 창 (시안 TitleBefore / TitleAfter / Login / LoginError / LoginOffline / Signup / SignupError).
    /// 시작 화면: 일러스트(로고 포함 한 장) + 오른쪽 위 설정·종료, 로그인 전에는 로그인 버튼·서버 상태·게스트로 플레이,
    /// 로그인 뒤에는 "> 화면을 클릭해 게임을 시작하세요"와 계정·로그아웃.
    /// 로그인 창(login.exe)·회원가입 창(signup.exe)은 입력칸 테두리 색으로 상태(포커스 하늘·맞음 초록·틀림 빨강)를 보이고,
    /// 아래 상태 줄에 서버 연결($ ping api)과 요청 결과를 터미널처럼 보여 준다
    /// </summary>
    public partial class MenuUI
    {
        public Action OnTitleStart, OnTitleLogin, OnTitleLogout, OnTitleGuest, OnSettings, OnTitleQuit;

        private GameObject _titleBefore, _titleAfter;
        private Text _titleServer, _titleAccount, _titleLogoutLabel;
        private Image _titleServerDot;
        private Button _titleTouch;

        /// <summary>
        /// ready=false: 로그인 버튼 + 서버 상태 + 왼쪽 아래 게스트로 플레이.
        /// ready=true (로그인 또는 게스트): "화면을 클릭해 게임을 시작하세요" + 왼쪽 아래 계정·로그아웃
        /// </summary>
        /// <param name="accountName">로그인 뒤 왼쪽 아래 이름 (게스트면 null)</param>
        public void ShowTitle(bool ready, string accountName, string logoutLabel)
        {
            HideAll();
            _title.SetActive(true);
            _titleBefore.SetActive(!ready);
            _titleAfter.SetActive(ready);
            _titleTouch.enabled = ready;
            _titleAccount.text = accountName == null ? "게스트로 플레이 중" : $"<b>{accountName}</b> 님으로 로그인";
            _titleLogoutLabel.text = logoutLabel;
            if (!ready) SetTitleServer(null, 0);
        }

        /// <summary>시작 화면 로그인 버튼 아래 서버 상태 (null = 확인 중)</summary>
        public void SetTitleServer(bool? reached, int ms)
        {
            if (_titleServer == null) return;
            _titleServerDot.color = reached == null ? Muted : reached.Value ? Green : Red;
            _titleServer.text = reached == null ? "서버 확인 중…" : reached.Value ? "서버 연결됨" : "서버에 연결할 수 없어요";
            RectTransform chip = (RectTransform)_titleServer.transform.parent;
            chip.sizeDelta = new Vector2(_titleServer.preferredWidth + 10 * S + 8 * S + 8 * S + 10 * S, 24 * S);
        }

        private GameObject BuildTitle()
        {
            var screen = Fill(_root, "Title", Night, raycast: true);
            Inset(screen, 0, 0, 0, 0);
            Cover(screen.rectTransform, ArtCatalog.Illustration("title_art"));
            // 로그인 뒤에는 화면 아무 데나 눌러 시작 (다른 버튼보다 뒤에 깔린 투명 버튼)
            _titleTouch = screen.gameObject.AddComponent<Button>();
            _titleTouch.transition = Selectable.Transition.None;
            _titleTouch.onClick.AddListener(() => OnTitleStart?.Invoke());

            // 오른쪽 위: 설정 · 종료
            RectTransform box = Frame(screen.transform, "System", Ink, WindowLine, 2 * S, 4 * S);
            At(box, 1148 * S, 22 * S, 108 * S, 98 * S);
            float rowH = (98 * S - 4 * S - 2 * S) / 2;
            TitleMenuRow(box, 2 * S, rowH, "desktop/settings", "설정", Fg, () => OnSettings?.Invoke());
            Image sep = Fill(box, "Line", TrackLine);
            At(sep, 2 * S, 2 * S + rowH, 104 * S, 2 * S);
            TitleMenuRow(box, 2 * S + rowH + 2 * S, rowH, "desktop/power", "종료", Red, () => OnTitleQuit?.Invoke());
            BorderOnTop(box);

            // ---- 로그인 전 ----
            _titleBefore = Node(screen.transform, "Before").gameObject;
            Inset(_titleBefore.transform, 0, 0, 0, 0);
            Transform before = _titleBefore.transform;
            PrimaryButton(before, "로그인", 36 * S, () => OnTitleLogin?.Invoke(), out RectTransform login);
            At(login, 490 * S, 418 * S, 300 * S, 80 * S);
            login.Find("Shadow").GetComponent<RectTransform>().offsetMin = new Vector2(6 * S, -6 * S);
            login.Find("Shadow").GetComponent<RectTransform>().offsetMax = new Vector2(6 * S, -6 * S);
            // 서버 상태 칩 (가운데)
            Image chip = Fill(before, "Server", Ink);
            chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0.5f, 1);
            chip.rectTransform.pivot = new Vector2(0.5f, 1);
            chip.rectTransform.anchoredPosition = new Vector2(0, -(418 * S + 80 * S + 12 * S));
            _titleServerDot = Fill(chip.transform, "Dot", Muted);
            _titleServerDot.rectTransform.anchorMin = _titleServerDot.rectTransform.anchorMax = new Vector2(0, 0.5f);
            _titleServerDot.rectTransform.pivot = new Vector2(0, 0.5f);
            _titleServerDot.rectTransform.anchoredPosition = new Vector2(10 * S, 0);
            _titleServerDot.rectTransform.sizeDelta = new Vector2(8 * S, 8 * S);
            _titleServer = Label(chip.transform, "", 12 * S, Muted, TextAnchor.MiddleLeft, UiKit.Mono);
            Inset(_titleServer, 10 * S + 8 * S + 8 * S, 0, 0, 0);
            SetTitleServer(null, 0);
            // 왼쪽 아래: 게스트로 플레이 (두 줄)
            RectTransform offline = TitleGhost(before, "게스트로 플레이", "로그인 없이 체험 · 기록은 이 PC에만", () => OnTitleGuest?.Invoke());
            At(offline, 24 * S, 634 * S, 214 * S, 56 * S);

            // ---- 로그인 뒤 ----
            _titleAfter = Node(screen.transform, "After").gameObject;
            Inset(_titleAfter.transform, 0, 0, 0, 0);
            Transform after = _titleAfter.transform;
            // 가운데: > 화면을 클릭해 게임을 시작하세요 ▮
            RectTransform prompt = Frame(after, "Start", Ink, Cyan, 2 * S, 6 * S);
            At(prompt, (1280 - 340) / 2f * S, 540 * S, 340 * S, 48 * S);
            Image promptHit = Fill(prompt, "Hit", new Color(0, 0, 0, 0));
            Inset(promptHit, 0, 0, 0, 0);
            Clickable(promptHit, () => OnTitleStart?.Invoke(), new Color(1, 1, 1, 0), new Color(1, 1, 1, 0));
            Text promptText = Label(prompt, "<color=#22d3ee>></color>  화면을 클릭해 게임을 시작하세요", 12 * S, Fg, TextAnchor.MiddleCenter);
            Inset(promptText, 0, 0, 16 * S, 0);
            Image promptCursor = Fill(prompt, "Cursor", Cyan);
            promptCursor.rectTransform.anchorMin = promptCursor.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            promptCursor.rectTransform.pivot = new Vector2(0, 0.5f);
            promptCursor.rectTransform.sizeDelta = new Vector2(7 * S, 14 * S);
            promptCursor.rectTransform.anchoredPosition = new Vector2(promptText.preferredWidth / 2 - 8 * S + 6 * S, 0);
            UiAnimator.Blink(promptCursor);
            BorderOnTop(prompt);
            // 왼쪽 아래: 계정 + 로그아웃
            RectTransform account = Node(after, "Account");
            At(account, 24 * S, 610 * S, 600 * S, 20 * S);
            RectTransform avatar = Node(account, "Avatar");
            At(avatar, 2 * S, 0, 20 * S, 20 * S);
            Image ring = Fill(avatar, "Ring", Ink);
            Inset(ring, -2 * S, -2 * S, -2 * S, -2 * S);
            Image head = Fill(avatar, "Head", Hex(0x2f2a2c));
            Inset(head, 0, 0, 0, 0);
            Image body = Fill(avatar, "Body", Hex(0xaeaaa8));
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = new Vector2(1, 0);
            body.rectTransform.offsetMin = Vector2.zero;
            body.rectTransform.offsetMax = new Vector2(0, 7 * S);
            Border(avatar, Dim, 2 * S);
            _titleAccount = Label(account, "", 12 * S, Fg, TextAnchor.MiddleLeft);
            Inset(_titleAccount, 2 * S + 20 * S + 8 * S, 0, 0, 0);
            Outlined(_titleAccount, 1.5f * S);
            RectTransform logout = TitleGhost(after, "로그아웃", null, () => OnTitleLogout?.Invoke());
            At(logout, 24 * S, 634 * S, 214 * S, 56 * S);
            _titleLogoutLabel = logout.Find("Label").GetComponent<Text>();
            return screen.gameObject;
        }

        private static void TitleMenuRow(RectTransform box, float top, float height, string icon, string label, Color color, Action onClick)
        {
            RectTransform row = Node(box, "Row:" + label);
            At(row, 2 * S, top, 104 * S, height);
            HoverArea(row, "Hover", Hex(0xeaf2ff, 0.08f), onClick, out Image area);
            Inset(area, 0, 0, 0, 0);
            Image ic = Icon(row, icon, 16 * S);
            if (ic != null) At(ic, 14 * S, (height - 16 * S) / 2, 16 * S, 16 * S);
            Text t = Label(row, label, 12 * S, color, TextAnchor.MiddleLeft, bold: true);
            Inset(t, 14 * S + 16 * S + 10 * S, 0, 0, 0);
        }

        /// <summary>시작 화면의 테두리 버튼 (짙은 바탕, 한 줄 또는 두 줄)</summary>
        private static RectTransform TitleGhost(Transform parent, string text, string sub, Action onClick)
        {
            RectTransform root = Node(parent, "Ghost:" + text);
            Image bg = Fill(root, "Bg", Ink);
            Inset(bg, 0, 0, 0, 0);
            HoverArea(root, "Hover", Hex(0x22d3ee, 0.15f), onClick, out Image area);
            Inset(area, 0, 0, 0, 0);
            Border(root, Cyan, 2 * S);
            Text label = Label(root, sub == null ? text : $"<b>{text}</b>\n<color=#22d3eecc>{sub}</color>", 12 * S, Cyan, TextAnchor.MiddleCenter,
                bold: sub == null);
            label.name = "Label";
            label.lineSpacing = 1.1f;
            Inset(label, 0, 0, 0, 0);
            return root;
        }

        /// <summary>시작 화면 종료: 예 / 아니요 (시안 PopExit)</summary>
        public void ShowQuitConfirm(Action quit)
        {
            ClosePopups();
            SystemDialog("시스템", "Executor를 종료할까요?", "진행 상황은 자동으로 저장됩니다.", "process executor.exe → exit(0)", true,
                ("아니요", ClosePopups, "ghost"), ("예", () => quit(), "danger"));
        }

        // ================= login.exe / signup.exe =================
        /// <summary>로그인 창이 쓰는 동작. 문자열 결과는 실패 문구 (성공이면 null)</summary>
        public class LoginActions
        {
            /// <summary>(이메일, 비밀번호, 로그인 유지)</summary>
            public Func<string, string, bool, Task> Login;
            public Func<string, string, bool, Task> Signup;
            /// <summary>이메일을 쓸 수 있는지 (null = 확인 실패)</summary>
            public Func<string, Task<bool?>> EmailAvailable;
            public Func<Task<(bool reached, int ms)>> Ping;
            public bool KeepLogin = true;
            public Action OnSuccess;
        }

        private class LoginState
        {
            public bool Signup;
            public string Email = "", Password = "", Confirm = "";
            public bool Keep = true;
            public string Error;
            public bool? Server;
            public int Ms;
            /// <summary>상태 줄: (명령, 결과, 좋음) — null이면 ping 결과</summary>
            public (string cmd, string result, bool good)? Status;
            public bool? EmailFree;
            public bool Busy;
        }

        private LoginState _login;
        private LoginActions _loginActions;

        public void ShowLogin(LoginActions actions)
        {
            _loginActions = actions;
            _login = new LoginState { Keep = actions.KeepLogin };
            DrawLogin();
            PingServer();
        }

        private async void PingServer()
        {
            if (_loginActions?.Ping == null) return;
            LoginState state = _login;
            var (reached, ms) = await _loginActions.Ping();
            if (state != _login) return;
            state.Server = reached;
            state.Ms = ms;
            SetTitleServer(reached, ms);
            if (state.Status == null) DrawLogin(keepFocus: true);
        }

        private const float LoginW = 380, FieldH = 36 * S, PadX = 22 * S;
        private InputField _loginFocusTarget;

        /// <summary>로그인·회원가입 창을 지금 상태로 다시 그린다 (오류 줄·모드에 따라 높이가 바뀐다)</summary>
        private void DrawLogin(bool keepFocus = false)
        {
            LoginState st = _login;
            InputField focusedBefore = keepFocus ? FocusedField() : null;
            string focusedName = focusedBefore != null ? focusedBefore.name : null;
            ClosePopups();
            float innerW = LoginW * S - WinBorder * 2 - PadX * 2;

            // 높이: 제목줄 + 위 18 + 칸들 + 버튼 + 아래 + 상태 줄
            float fields = st.Signup ? 3 * (16 * S + 6 * S + FieldH + 6 * S + 16 * S) + 2 * 12 * S
                : 2 * (16 * S + 6 * S + FieldH) + 12 * S + (st.Error != null ? 8 * S + 16 * S : 0) + 12 * S + 16 * S;
            float bodyH = 18 * S + fields + 12 * S + 44 * S + (st.Signup ? 12 * S + 16 * S : 0) + 16 * S + 32 * S;
            float heightPx = WinBorder * 2 + WinTitleH + bodyH;
            float top = (720 - heightPx / S) / 2 - 20;
            WindowFrame(_popupRoot, st.Signup ? "signup.exe" : "login.exe", null, 450, top, LoginW, heightPx / S, true, CloseLogin,
                out RectTransform body, out GameObject blocker);
            // 창 버튼은 닫기만 (최소화·최대화 모양 숨김)
            Transform titleBar = blocker.transform.Find("Window/TitleBar");
            if (titleBar != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    Transform b = titleBar.Find("WindowButton" + i);
                    if (b != null) b.gameObject.SetActive(false);
                }
            }

            float y = 18 * S;
            InputField email = LoginField(body, "Email", "이메일", "name@example.com", false, ref y, innerW, st.Email, v => st.Email = v);
            InputField password, confirm = null;
            Text emailHint = null, pwHint = null, confirmHint = null;
            if (st.Signup)
            {
                emailHint = Hint(body, ref y, innerW);
                y += 12 * S;
                password = LoginField(body, "Password", "비밀번호", "8자 이상", true, ref y, innerW, st.Password, v => st.Password = v);
                pwHint = Hint(body, ref y, innerW);
                y += 12 * S;
                confirm = LoginField(body, "Confirm", "비밀번호 재입력", "비밀번호를 한 번 더", true, ref y, innerW, st.Confirm, v => st.Confirm = v);
                confirmHint = Hint(body, ref y, innerW);
            }
            else
            {
                y += 12 * S;
                password = LoginField(body, "Password", "비밀번호", "비밀번호", true, ref y, innerW, st.Password, v => st.Password = v);
                if (st.Error != null)
                {
                    y += 8 * S;
                    Text err = Label(body, st.Error, 12 * S, Red, TextAnchor.MiddleLeft);
                    At(err, PadX, y, innerW, 16 * S);
                    y += 16 * S;
                    password.GetComponent<FieldFrame>().State = FieldFrame.Mark.Bad;
                }
                y += 12 * S;
                // 로그인 유지 ✓ / 계정 만들기
                RectTransform keep = Node(body, "Keep");
                At(keep, PadX, y, innerW / 2, 16 * S);
                HoverArea(keep, "Hover", new Color(0, 0, 0, 0), () => { st.Keep = !st.Keep; DrawLogin(true); }, out Image keepArea);
                Inset(keepArea, 0, 0, 0, 0);
                Image boxBg = Fill(keep, "Box", st.Keep ? Cyan : Night);
                At(boxBg, 0, 1 * S, 14 * S, 14 * S);
                if (!st.Keep) Border(boxBg.transform, Muted, 1 * S);
                else
                {
                    Image tick = Icon(boxBg.transform, "desktop/check", 10 * S);
                    if (tick != null) At(tick, 2 * S, 2 * S, 10 * S, 10 * S);
                }
                Text keepText = Label(keep, "로그인 유지", 12 * S, Muted, TextAnchor.MiddleLeft);
                Inset(keepText, 14 * S + 8 * S, 0, 0, 0);
                RectTransform make = Node(body, "ToSignup");
                At(make, PadX + innerW / 2, y, innerW / 2, 16 * S);
                HoverArea(make, "Hover", new Color(0, 0, 0, 0), () => SwitchLogin(true), out Image makeArea);
                Inset(makeArea, 0, 0, 0, 0);
                Text makeText = Label(make, "계정 만들기", 12 * S, Cyan, TextAnchor.MiddleRight);
                Inset(makeText, 0, 0, 0, 0);
                y += 16 * S;
            }

            // 주 버튼
            y += 12 * S;
            bool serverDown = st.Server == false;
            Button submit = PrimaryButton(body, st.Signup ? "가입하기" : "로그인", 24 * S, () => SubmitLogin(), out RectTransform submitRt);
            At(submitRt, PadX, y, innerW, 44 * S);
            y += 44 * S;
            if (st.Signup)
            {
                y += 12 * S;
                Text back = Label(body, "이미 계정이 있나요? <color=#22d3ee>로그인</color>", 12 * S, Muted, TextAnchor.MiddleCenter);
                At(back, PadX, y, innerW, 16 * S);
                back.raycastTarget = true;
                Clickable(back, () => SwitchLogin(false), Color.white, new Color(1.2f, 1.2f, 1.2f));
            }

            // 상태 줄
            float statusTop = bodyH - 32 * S;
            Image line = Fill(body, "StatusLine", TrackLine);
            At(line, 0, statusTop, LoginW * S - WinBorder * 2, 2 * S);
            var (cmd, result, good) = st.Status ?? (st.Server == null ? ("$ ping api", "확인 중…", true)
                : st.Server.Value ? ("$ ping api", $"서버 연결됨 · {st.Ms}ms", true) : ("$ ping api", "[ERR] 서버에 연결할 수 없어요", false));
            Color statusColor = st.Status == null && st.Server == null ? Muted : good ? Green : Red;
            Image dot = Fill(body, "Dot", statusColor);
            At(dot, 12 * S, statusTop + 2 * S + 11 * S, 8 * S, 8 * S);
            Text status = Label(body, $"<color=#8b98b8>{cmd}</color>  {result}", 12 * S, statusColor, TextAnchor.MiddleLeft, UiKit.Mono);
            At(status, 12 * S + 8 * S + 8 * S, statusTop + 2 * S, innerW, 30 * S);
            if (good && st.Server == true)
            {
                Image cursor = Fill(body, "Cursor", Cyan);
                At(cursor, 12 * S + 8 * S + 8 * S + status.preferredWidth + 6 * S, statusTop + 2 * S + 9 * S, 7 * S, 12 * S);
                UiAnimator.Blink(cursor);
            }

            // 실시간 검사 (회원가입)
            void Validate()
            {
                if (!st.Signup) { submit.interactable = !serverDown && !st.Busy; DisableLook(submitRt, !submit.interactable); return; }
                bool emailFormat = Regex.IsMatch(st.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                SetHint(emailHint, email, st.Email.Length == 0 ? null
                    : !emailFormat ? (false, "이메일 형식이 올바르지 않아요.")
                    : st.EmailFree == true ? (true, "사용할 수 있는 이메일이에요")
                    : st.EmailFree == false ? (false, "이미 가입된 이메일이에요. 로그인해 주세요.") : ((bool, string)?)null);
                bool pwOk = PasswordOk(st.Password);
                SetHint(pwHint, password, st.Password.Length == 0 ? ((bool, string)?)null : (pwOk, pwOk ? "8자 이상 · 영문과 숫자 포함" : "8자 이상, 영문과 숫자를 함께 넣어 주세요."),
                    st.Password.Length == 0 ? "8자 이상 · 영문과 숫자 포함" : null);
                bool same = st.Confirm.Length > 0 && st.Confirm == st.Password;
                SetHint(confirmHint, confirm, st.Confirm.Length == 0 ? ((bool, string)?)null : (same, same ? "비밀번호가 일치해요" : "비밀번호가 일치하지 않아요."));
                submit.interactable = !serverDown && !st.Busy && emailFormat && st.EmailFree != false && pwOk && same;
                DisableLook(submitRt, !submit.interactable);
            }
            email.onValueChanged.AddListener(_ => { st.EmailFree = null; Validate(); });
            password.onValueChanged.AddListener(_ => Validate());
            if (confirm != null) confirm.onValueChanged.AddListener(_ => Validate());
            if (st.Signup) email.onEndEdit.AddListener(_ => CheckEmail(Validate));
            Validate();

            // Enter: 다음 칸으로, 마지막 칸이면 제출
            UiKit.OnEnter(email, () => FocusNext(email.text.Trim().Length == 0 ? email : password));
            UiKit.OnEnter(password, () =>
            {
                if (password.text.Length == 0) FocusNext(password);
                else if (st.Signup) FocusNext(confirm);
                else SubmitLogin();
            });
            if (confirm != null) UiKit.OnEnter(confirm, SubmitLogin);

            InputField target = focusedName == "Confirm" ? confirm : focusedName == "Password" ? password : focusedName == "Email" ? email
                : _loginFocusTarget != null ? FieldNamed(_loginFocusTarget.name, email, password, confirm)
                : st.Email.Length == 0 ? email : password;
            _loginFocusTarget = null;
            if (target != null) FocusNext(target);
        }

        private static InputField FieldNamed(string name, InputField email, InputField password, InputField confirm) =>
            name == "Confirm" ? confirm : name == "Password" ? password : email;

        private static InputField FocusedField()
        {
            var selected = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            return selected != null ? selected.GetComponent<InputField>() : null;
        }

        public static bool PasswordOk(string pw) =>
            pw != null && pw.Length >= 8 && pw.Length <= 64 && Regex.IsMatch(pw, "[A-Za-z]") && Regex.IsMatch(pw, "[0-9]");

        private static void DisableLook(RectTransform button, bool disabled)
        {
            Transform face = button.Find("Face");
            Transform shadow = button.Find("Shadow");
            if (shadow != null) shadow.gameObject.SetActive(!disabled);
            var b = face != null ? face.GetComponent<Button>() : null;
            if (b != null)
            {
                var colors = b.colors;
                colors.disabledColor = Track;
                b.colors = colors;
            }
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = disabled ? Dim : Ink;
        }

        private async void CheckEmail(Action revalidate)
        {
            LoginState st = _login;
            string email = st.Email.Trim();
            if (_loginActions?.EmailAvailable == null || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) return;
            bool? free = await _loginActions.EmailAvailable(email);
            if (st != _login || st.Email.Trim() != email) return;
            st.EmailFree = free;
            revalidate();
        }

        private void SwitchLogin(bool signup)
        {
            _login.Signup = signup;
            _login.Error = null;
            _login.Status = null;
            _login.Confirm = "";
            _login.EmailFree = null;
            DrawLogin();
            if (signup && _login.Email.Length > 0) CheckEmail(() => DrawLogin(true));
        }

        private void CloseLogin()
        {
            _login = null;
            ClosePopups();
        }

        private async void SubmitLogin()
        {
            LoginState st = _login;
            if (st == null || st.Busy || st.Server == false) return;
            string email = st.Email.Trim();
            if (email.Length == 0 || st.Password.Length == 0) return;
            if (st.Signup && !(PasswordOk(st.Password) && st.Password == st.Confirm)) return;
            st.Busy = true;
            st.Status = (st.Signup ? "$ POST /auth/signup" : "$ auth --jwt", "요청 중…", true);
            DrawLogin(true);
            try
            {
                if (st.Signup) await _loginActions.Signup(email, st.Password, st.Keep);
                else await _loginActions.Login(email, st.Password, st.Keep);
                if (st != _login) return;
                CloseLogin();
                _loginActions.OnSuccess?.Invoke();
            }
            catch (Network.ApiException e)
            {
                if (st != _login) return;
                st.Busy = false;
                if (e.IsNetworkError)
                {
                    st.Server = false;
                    st.Status = null;
                    st.Error = st.Signup ? null : "서버에 연결할 수 없어요.";
                }
                else if (st.Signup)
                {
                    if (e.Code == "DUPLICATE_EMAIL") st.EmailFree = false;
                    st.Status = ("$ POST /auth/signup", $"[{e.Status}] {(e.Code == "DUPLICATE_EMAIL" ? "이미 가입된 이메일" : e.Message)}", false);
                }
                else
                {
                    bool wrong = e.Code == "INVALID_CREDENTIALS" || e.Status == 401;
                    st.Error = wrong ? "이메일 또는 비밀번호가 맞지 않아요." : e.Message;
                    st.Status = ("$ auth --jwt", wrong ? $"[{e.Status}] 인증 실패" : $"[{e.Status}] {e.Message}", false);
                    _loginFocusTarget = null;
                }
                DrawLogin();
            }
        }

        /// <summary>입력칸: 위 이름 + 칸 (테두리 색으로 상태). 바뀐 값은 onChange로 상태에 저장</summary>
        private static InputField LoginField(RectTransform body, string name, string label, string placeholder, bool password, ref float y,
            float width, string value, Action<string> onChange)
        {
            Text l = Label(body, label, 12 * S, Muted, TextAnchor.MiddleLeft);
            At(l, PadX, y, width, 16 * S);
            y += 16 * S + 6 * S;
            InputField field = UiKit.Input(body, placeholder, password, password ? 64 : 255, (int)(12 * S));
            field.name = name;
            At(field, PadX, y, width, FieldH);
            FieldFrame.Apply(field);
            field.text = value;
            field.onValueChanged.AddListener(v => onChange(v));
            y += FieldH;
            return field;
        }

        /// <summary>칸 아래 안내 줄 (아이콘 + 글자)</summary>
        private static Text Hint(RectTransform body, ref float y, float width)
        {
            y += 6 * S;
            RectTransform row = Node(body, "Hint");
            At(row, PadX, y, width, 16 * S);
            Image icon = Icon(row, "desktop/check_white", 10 * S);
            if (icon != null) { icon.name = "Mark"; At(icon, 0, 3 * S, 11 * S, 10 * S); }
            Text t = Label(row, "", 12 * S, Muted, TextAnchor.MiddleLeft);
            Inset(t, 16 * S, 0, 0, 0);
            y += 16 * S;
            return t;
        }

        /// <summary>안내 줄과 칸 테두리: (맞음, 글) / null이면 neutral 글(회색) 또는 비움</summary>
        private static void SetHint(Text hint, InputField field, (bool ok, string text)? state, string neutral = null)
        {
            if (hint == null) return;
            Transform mark = hint.transform.parent.Find("Mark");
            var frame = field.GetComponent<FieldFrame>();
            if (state == null)
            {
                hint.text = neutral ?? "";
                hint.color = Muted;
                hint.rectTransform.offsetMin = new Vector2(0, 0);
                if (mark != null) mark.gameObject.SetActive(false);
                if (frame != null) frame.State = FieldFrame.Mark.None;
                return;
            }
            var (ok, text) = state.Value;
            hint.text = text;
            hint.color = ok ? Green : Red;
            hint.rectTransform.offsetMin = new Vector2(ok ? 16 * S : 0, 0);
            if (mark != null)
            {
                mark.gameObject.SetActive(ok);
                mark.GetComponent<Image>().color = Green;
            }
            if (frame != null) frame.State = ok ? FieldFrame.Mark.Good : FieldFrame.Mark.Bad;
        }

        /// <summary>입력칸으로 포커스를 옮긴다: 바로 옮기고, 한 프레임 뒤에도 그 칸이 아니면 한 번 더 (Enter 처리와 겹칠 때 대비)</summary>
        private void FocusNext(InputField field)
        {
            if (field == null) return;
            UiKit.FocusField(field);
            StartCoroutine(FocusRoutine(field));
        }

        private static IEnumerator FocusRoutine(InputField field)
        {
            yield return null;
            if (field == null || !field.gameObject.activeInHierarchy || field.isFocused) yield break;
            UiKit.FocusField(field);
        }
    }
}
