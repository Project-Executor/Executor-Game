using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 설정 창 (시안 Settings, settings.exe). 메뉴와 게임 화면이 같이 쓴다.
    /// 왼쪽 탭: 사운드·화면(볼륨·타자 효과음·화면 테마·화면 모드·글리치·남은 턴 게이지, 기본값으로·저장) / 조작(키 안내) / 계정(로그아웃·버그 제보·내 신고).
    /// 라이트 테마·글리치 효과·키 바꾸기는 업데이트 예정
    /// </summary>
    public static class SettingsWindow
    {
        public class Actions
        {
            public string Account;
            public bool LoggedIn;
            public Action Logout, BugReport, MyReports;
        }

        private const float W = 740, H = 507, NavW = 170 * S;

        /// <summary>저장 전에 바꾸는 값</summary>
        private class Draft
        {
            public float Master, Bgm, Sfx;
            public bool Typing, Gauge, Fullscreen;

            public static Draft Load() => new Draft
            {
                Master = GameSettings.Master, Bgm = GameSettings.Bgm, Sfx = GameSettings.Sfx,
                Typing = GameSettings.TypingSound, Gauge = GameSettings.TurnGauge, Fullscreen = GameSettings.Fullscreen,
            };

            public static Draft Default() => new Draft { Master = 0.8f, Bgm = 0.6f, Sfx = 0.9f, Typing = true, Gauge = true, Fullscreen = true };
        }

        public static GameObject Open(Transform root, Actions actions, string tab = "screen")
        {
            GameObject blocker = null;
            WindowFrame(root, "settings.exe", null, 270, 90, W, H, true, () => UnityEngine.Object.Destroy(blocker), out RectTransform body, out blocker);
            HideMinMax(blocker);
            var draft = Draft.Load();
            float bodyH = H * S - WinBorder * 2 - WinTitleH;

            Image nav = Fill(body, "Nav", Night);
            At(nav, 0, 0, NavW - 2 * S, bodyH);
            Image navLine = Fill(body, "NavLine", TrackLine);
            At(navLine, NavW - 2 * S, 0, 2 * S, bodyH);
            RectTransform pane = Node(body, "Pane");
            At(pane, NavW + 24 * S, 20 * S, W * S - WinBorder * 2 - NavW - 48 * S, bodyH - 40 * S);

            void Show(string t)
            {
                UiKit.Clear(nav.transform);
                float y = 12 * S;
                foreach (var (key, label) in new[] { ("screen", "사운드 · 화면"), ("keys", "조작"), ("account", "계정") })
                {
                    string k = key;
                    RectTransform row = Node(nav.transform, "Tab:" + label);
                    At(row, 0, y, NavW - 2 * S, 40 * S);
                    bool on = k == t;
                    if (on)
                    {
                        Image bg = Fill(row, "Bg", Hex(0x22d3ee, 0.14f));
                        Inset(bg, 0, 0, 0, 0);
                        Image bar = Fill(row, "Bar", Cyan);
                        At(bar, 0, 0, 2 * S, 40 * S);
                    }
                    else
                    {
                        HoverArea(row, "Hover", Hex(0x22d3ee, 0.10f), () => Show(k), out Image area);
                        Inset(area, 0, 0, 0, 0);
                    }
                    Text text = Label(row, label, 12 * S, on ? Fg : Muted, TextAnchor.MiddleLeft, bold: true);
                    Inset(text, 16 * S, 0, 0, 0);
                    y += 40 * S;
                }
                UiKit.Clear(pane);
                if (t == "keys") DrawKeys(pane);
                else if (t == "account") DrawAccount(pane, actions, () => UnityEngine.Object.Destroy(blocker));
                else DrawScreen(pane, draft, () => UnityEngine.Object.Destroy(blocker), d => { draft = d; Show("screen"); });
            }
            Show(tab);
            return blocker;
        }

        private static void HideMinMax(GameObject blocker)
        {
            Transform titleBar = blocker.transform.Find("Window/TitleBar");
            if (titleBar == null) return;
            for (int i = 0; i < 2; i++)
            {
                Transform b = titleBar.Find("WindowButton" + i);
                if (b != null) b.gameObject.SetActive(false);
            }
        }

        // ---------------- 사운드 · 화면 ----------------
        private static void DrawScreen(RectTransform pane, Draft d, Action close, Action<Draft> redraw)
        {
            float w = pane.sizeDelta.x, y = 0;
            Section(pane, "사운드", ref y, w);
            VolumeRow(pane, "전체 볼륨", d.Master, v => d.Master = v, ref y, w);
            VolumeRow(pane, "배경음", d.Bgm, v => d.Bgm = v, ref y, w);
            VolumeRow(pane, "효과음", d.Sfx, v => d.Sfx = v, ref y, w);
            ToggleRow(pane, "타자 효과음", "해킹할 때 키보드 소리", d.Typing, v => d.Typing = v, true, ref y, w);
            y += 8 * S;
            Section(pane, "화면", ref y, w);
            ChoiceRow(pane, "화면 테마", new[] { ("다크", true, true), ("라이트 (업데이트 예정)", false, false) }, _ => { }, ref y, w);
            ChoiceRow(pane, "화면 모드", new[] { ("창", !d.Fullscreen, true), ("전체 화면", d.Fullscreen, true) },
                i => { d.Fullscreen = i == 1; redraw(d); }, ref y, w);
            ToggleRow(pane, "글리치 효과", "깨짐·색수차 연출 (업데이트 예정)", false, _ => { }, false, ref y, w);
            ToggleRow(pane, "남은 턴 게이지 표시", null, d.Gauge, v => d.Gauge = v, true, ref y, w);

            // 기본값으로 / 저장
            float by = pane.sizeDelta.y - 44 * S;
            PrimaryButton(pane, "저장", 12 * S, () =>
            {
                GameSettings.Master = d.Master;
                GameSettings.Bgm = d.Bgm;
                GameSettings.Sfx = d.Sfx;
                GameSettings.TypingSound = d.Typing;
                GameSettings.TurnGauge = d.Gauge;
                bool screenChanged = GameSettings.Fullscreen != d.Fullscreen;
                GameSettings.Fullscreen = d.Fullscreen;
                GameSettings.Save();
                if (screenChanged) GameSettings.ApplyScreen();
                GameSettings.NotifyChanged();
                close();
            }, out RectTransform save);
            AtRight(save, 0, by, 140 * S, 44 * S);
            GhostButton(pane, "기본값으로", 12 * S, () => redraw(Draft.Default()), out RectTransform reset);
            AtRight(reset, 140 * S + 10 * S, by, 110 * S, 44 * S);
        }

        private static void Section(RectTransform pane, string title, ref float y, float w)
        {
            Text t = Label(pane, title, 12 * S, Muted, TextAnchor.MiddleLeft, bold: true);
            At(t, 0, y, w, 16 * S);
            y += 16 * S + 14 * S;
        }

        private static void VolumeRow(RectTransform pane, string label, float value, Action<float> onChange, ref float y, float w)
        {
            const float rowH = 20 * S;
            Text l = Label(pane, label, 12 * S, Fg, TextAnchor.MiddleLeft);
            At(l, 0, y, 96 * S, rowH);
            Text num = Label(pane, Mathf.RoundToInt(value * 100).ToString(), 12 * S, Muted, TextAnchor.MiddleRight, UiKit.Mono);
            At(num, w - 44 * S, y, 44 * S, rowH);
            float trackW = w - 96 * S - 14 * S - 44 * S - 14 * S;
            RectTransform slider = Node(pane, "Slider:" + label);
            At(slider, 96 * S + 14 * S, y, trackW, rowH);
            Image track = Fill(slider, "Track", Track, raycast: true);
            At(track, 0, (rowH - 8 * S) / 2, trackW, 8 * S);
            Border(track.transform, TrackLine, 1 * S);
            Image fill = Fill(track.transform, "Fill", Cyan);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            Image handle = Fill(slider, "Handle", Fg, raycast: true);
            void Set(float v)
            {
                v = Mathf.Clamp01(v);
                fill.rectTransform.anchorMax = new Vector2(v, 1);
                At(handle, v * trackW - 8 * S, 0, 16 * S, rowH);
                num.text = Mathf.RoundToInt(v * 100).ToString();
                onChange(v);
            }
            Set(value);
            Border(handle.transform, Ink, 2 * S);
            // 누르거나 끌면 그 자리 값
            var drag = slider.gameObject.AddComponent<SliderDrag>();
            drag.OnValue = Set;
            Image hit = slider.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            y += rowH + 14 * S;
        }

        private static void ToggleRow(RectTransform pane, string label, string sub, bool value, Action<bool> onChange, bool enabled, ref float y, float w)
        {
            float rowH = sub != null ? 32 * S : 24 * S;
            Text l = Label(pane, sub == null ? label : $"{label}\n<color=#4b5675>{sub}</color>", 12 * S, enabled ? Fg : Muted, TextAnchor.MiddleLeft);
            l.lineSpacing = 1.1f;
            At(l, 0, y, w - 60 * S, rowH);
            RectTransform sw = Node(pane, "Switch:" + label);
            At(sw, w - 44 * S, y + (rowH - 24 * S) / 2, 44 * S, 24 * S);
            bool on = value;
            void Draw()
            {
                UiKit.Clear(sw);
                Image bg = Fill(sw, "Bg", on ? Cyan : Night, raycast: enabled);
                Inset(bg, 0, 0, 0, 0);
                Border(bg.transform, on ? Cyan : TrackLine, 2 * S);
                Image knob = Fill(sw, "Knob", on ? Ink : Dim);
                At(knob, on ? 22 * S : 2 * S, 2 * S, 20 * S, 20 * S);
                if (enabled) Clickable(bg, () => { on = !on; onChange(on); Draw(); }, on ? Cyan : Night, Color.Lerp(on ? Cyan : Night, Color.white, 0.2f));
                if (!enabled) bg.color = Hex(0x15112e);
            }
            Draw();
            y += rowH + 14 * S;
        }

        /// <summary>여러 개 중 하나 (켜진 것 하늘색 채움). enabled=false면 회색으로 누를 수 없음</summary>
        private static void ChoiceRow(RectTransform pane, string label, (string text, bool on, bool enabled)[] options, Action<int> onPick, ref float y, float w)
        {
            const float rowH = 32 * S;
            Text l = Label(pane, label, 12 * S, Fg, TextAnchor.MiddleLeft);
            At(l, 0, y, w / 2, rowH);
            float right = 0;
            for (int i = options.Length - 1; i >= 0; i--)
            {
                var (text, on, enabled) = options[i];
                Text t = Label(pane, text, 12 * S, on ? Ink : enabled ? Muted : Dim, TextAnchor.MiddleCenter, bold: true);
                float bw = t.preferredWidth + 28 * S;
                Image bg = Fill(pane, "Choice:" + text, on ? Cyan : new Color(0, 0, 0, 0), raycast: enabled && !on);
                AtRight(bg, right, y, bw, rowH);
                if (!on) Border(bg.transform, TrackLine, 2 * S);
                int index = i;
                if (enabled && !on) Clickable(bg, () => onPick(index), new Color(0, 0, 0, 0), Hex(0x22d3ee, 0.15f));
                t.transform.SetParent(bg.transform, false);
                Inset(t, 0, 0, 0, 0);
                right += bw + 4 * S;
            }
            y += rowH + 14 * S;
        }

        // ---------------- 조작 ----------------
        private static void DrawKeys(RectTransform pane)
        {
            float w = pane.sizeDelta.x, y = 0;
            Section(pane, "키보드", ref y, w);
            foreach (var (key, what) in new[]
                     {
                         ("W A S D / 방향키", "이동"), ("E", "인물과 대화 · 감염된 컴퓨터 해킹"), ("Q", "도착한 체크포인트로 워프"), ("M", "맵 전체 보기"),
                         ("R", "다시 하기"), ("Esc", "일시정지 · 창 닫기"), ("Space / Enter", "다음 대사 · 확인"), ("W · S", "대화 선택지 고르기"),
                     })
            {
                Text k = Label(pane, key, 12 * S, Fg, TextAnchor.MiddleLeft, UiKit.Mono);
                At(k, 0, y, 200 * S, 24 * S);
                Text d = Label(pane, what, 12 * S, Muted, TextAnchor.MiddleLeft);
                At(d, 200 * S, y, w - 200 * S, 24 * S);
                y += 24 * S + 8 * S;
            }
            Text note = Label(pane, "키 바꾸기는 업데이트 예정이에요.", 12 * S, Dim, TextAnchor.MiddleLeft);
            At(note, 0, y + 8 * S, w, 16 * S);
        }

        // ---------------- 계정 ----------------
        private static void DrawAccount(RectTransform pane, Actions actions, Action close)
        {
            float w = pane.sizeDelta.x, y = 0;
            Section(pane, "계정", ref y, w);
            Text acc = Label(pane, $"{actions.Account}\n<color=#4b5675>서버 {Network.ApiConfig.BaseUrl}</color>", 12 * S, Fg, TextAnchor.UpperLeft);
            acc.lineSpacing = 1.4f;
            acc.horizontalOverflow = HorizontalWrapMode.Wrap;
            At(acc, 0, y, w, 48 * S);
            y += 48 * S + 20 * S;
            if (actions.LoggedIn)
            {
                Section(pane, "신고 · 문의", ref y, w);
                if (actions.BugReport != null)
                {
                    GhostButton(pane, "버그 제보", 12 * S, () => { close(); actions.BugReport(); }, out RectTransform bug);
                    At(bug, 0, y, 140 * S, 40 * S);
                }
                if (actions.MyReports != null)
                {
                    GhostButton(pane, "내 신고", 12 * S, () => { close(); actions.MyReports(); }, out RectTransform mine);
                    At(mine, 150 * S, y, 140 * S, 40 * S);
                }
                y += 40 * S + 24 * S;
                if (actions.Logout != null)
                {
                    GhostButton(pane, "로그아웃", 12 * S, () => { close(); actions.Logout(); }, out RectTransform logout);
                    At(logout, 0, pane.sizeDelta.y - 44 * S, 140 * S, 44 * S);
                }
            }
            else
            {
                Text t = Label(pane, "버그 제보와 내 신고는 로그인하면 쓸 수 있어요.", 12 * S, Muted, TextAnchor.MiddleLeft);
                At(t, 0, y, w, 16 * S);
            }
        }
    }
}
