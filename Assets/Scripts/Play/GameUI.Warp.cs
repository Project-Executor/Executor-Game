using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 워프 창 (시안 Warp, warp.exe — 체크포인트로 돌아가기). 시작 → 체크포인트들 → 출구를 가로 카드로 잇고,
    /// 카드마다 안정·불안정·미도착과 "구간 최고 / 기준 턴". 도착한 곳만 고를 수 있다 (← → 또는 클릭). 아래에 고른 곳 설명과 워프 안내
    /// </summary>
    public partial class GameUI
    {
        public class WarpCard
        {
            public int Order;
            public string Name;
            /// <summary>"구간 1" (출구는 빈칸)</summary>
            public string Segment;
            /// <summary>null 미도착, true 안정, false 불안정</summary>
            public bool? Stable;
            /// <summary>"11 / 15" (기록 없으면 "— / 15", 출구는 빈칸)</summary>
            public string Record;
            /// <summary>워프할 수 있는지 (도착한 체크포인트·시작)</summary>
            public bool Reachable;
            /// <summary>고른 곳 설명 (색 태그 포함)</summary>
            public string Detail;
        }

        private List<WarpCard> _warpCards;
        private int _warpSelected = -1;
        private Action<int> _onWarp;

        public bool IsWarpOpen => _warpCards != null && _popupRoot.childCount > 0;

        public void ShowWarp(List<WarpCard> cards, int selectedOrder, Action<int> onWarp)
        {
            _warpCards = cards;
            _onWarp = onWarp;
            _warpSelected = Mathf.Max(0, cards.FindIndex(c => c.Order == selectedOrder && c.Reachable));
            DrawWarp();
        }

        /// <summary>← → : 도착한 곳 사이를 옮긴다</summary>
        public void MoveWarpSelection(int step)
        {
            if (!IsWarpOpen) return;
            int i = _warpSelected;
            for (int n = 0; n < _warpCards.Count; n++)
            {
                i += step;
                if (i < 0 || i >= _warpCards.Count) return;
                if (_warpCards[i].Reachable) { _warpSelected = i; DrawWarp(); return; }
            }
        }

        public void ConfirmWarp()
        {
            if (!IsWarpOpen || _warpSelected < 0) return;
            int order = _warpCards[_warpSelected].Order;
            CloseWarp();
            _onWarp?.Invoke(order);
        }

        private void CloseWarp()
        {
            _warpCards = null;
            ClosePopups();
        }

        private void DrawWarp()
        {
            List<WarpCard> cards = _warpCards;
            ClosePopups();
            _warpCards = cards;
            const float width = 800, bodyH = 22 + 16 + 18 + 120 + 18 + 84 + 18 + 44 + 22;
            WindowFrame(_popupRoot, "warp.exe — 체크포인트로 돌아가기", null, 240, 130, width, 34 + 4 + bodyH, true, CloseWarp,
                out RectTransform body, out GameObject blocker);
            Transform bar = blocker.transform.Find("Window/TitleBar");
            for (int i = 0; i < 2 && bar != null; i++) bar.Find("WindowButton" + i)?.gameObject.SetActive(false);
            float w = width * S - WinBorder * 2 - 48 * S, y = 22 * S;
            Text intro = Label(body, "도착한 곳으로만 워프할 수 있어요 · 기록은 <color=#eaf2ff>구간 최고 / 기준 턴</color>", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(intro, 24 * S, y, w, 16 * S);
            y += 16 * S + 18 * S;

            // 카드 줄
            int count = _warpCards.Count;
            const float card = 120 * S;
            float gap = count > 1 ? (w - card * count) / (count - 1) : 0;
            for (int i = 0; i < count; i++)
            {
                WarpCard c = _warpCards[i];
                float x = 24 * S + i * (card + gap);
                if (i + 1 < count)
                {
                    bool lit = c.Reachable && _warpCards[i + 1].Reachable;
                    Image link = Fill(body, "Link", lit ? Cyan : TrackLine);
                    At(link, x + card, y + card / 2 - 1.5f * S, gap, 3 * S);
                }
                bool selected = i == _warpSelected;
                RectTransform box = Node(body, "Card" + c.Order);
                At(box, x, y, card, card);
                if (selected)
                {
                    Image glow = Fill(box, "Glow", Hex(0x22d3ee, 0.25f));
                    Inset(glow, -4 * S, -4 * S, -4 * S, -4 * S);
                }
                Color bg = selected ? Cyan : c.Reachable ? Night : Ink;
                Color fg = selected ? Ink : c.Reachable ? Fg : Dim;
                Image face = Fill(box, "Face", bg, raycast: c.Reachable);
                Inset(face, 0, 0, 0, 0);
                int index = i;
                if (c.Reachable && !selected)
                    Clickable(face, () => { _warpSelected = index; DrawWarp(); }, bg, Color.Lerp(bg, Color.white, 0.1f));
                if (!selected) Border(box, c.Stable == true && c.Reachable ? Green : TrackLine, 2 * S);
                Text name = Label(box, c.Name, 12 * S, fg, TextAnchor.MiddleLeft, bold: true);
                At(name, 12 * S, 12 * S, card - 24 * S, 16 * S);
                Text seg = Label(box, c.Segment ?? "", 12 * S, fg, TextAnchor.MiddleLeft);
                seg.color = new Color(fg.r, fg.g, fg.b, c.Reachable ? 0.8f : 1f);
                At(seg, 12 * S, 12 * S + 16 * S + 8 * S, card - 24 * S, 16 * S);
                Color stateColor = selected ? Ink : c.Stable == null ? Dim : c.Stable.Value ? Green : Red;
                string state = c.Stable == null ? "미도착" : c.Stable.Value ? "안정" : "불안정";
                Image dot = Fill(box, "Dot", stateColor);
                At(dot, 12 * S, 12 * S + 48 * S + 4 * S, 8 * S, 8 * S);
                Text st = Label(box, state, 12 * S, stateColor, TextAnchor.MiddleLeft);
                At(st, 12 * S + 14 * S, 12 * S + 48 * S, card - 38 * S, 16 * S);
                Text rec = Label(box, c.Record ?? "", 12 * S, fg, TextAnchor.MiddleLeft, UiKit.Mono);
                At(rec, 12 * S, card - 12 * S - 16 * S, card - 24 * S, 16 * S);
            }
            y += card + 18 * S;

            // 설명 두 칸
            float half = (w - 12 * S) / 2;
            WarpCard sel = _warpSelected >= 0 ? _warpCards[_warpSelected] : null;
            Image left = Fill(body, "Detail", Night);
            At(left, 24 * S, y, half, 84 * S);
            Text detail = Label(left.transform, sel?.Detail ?? "", 12 * S, Fg, TextAnchor.UpperLeft);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.lineSpacing = 1.4f;
            Inset(detail, 14 * S, 12 * S, 14 * S, 8 * S);
            Image right = Fill(body, "Rule", Night);
            At(right, 24 * S + half + 12 * S, y, half, 84 * S);
            Text rule = Label(right.transform, "<b>워프하면</b>\n마지막 도착 때 맵 상태로 돌아가요. 턴은 쓰지 않아요.\n<color=#8b98b8>유지: 데이터 파편 · 도감 · 밝힌 미니맵</color>",
                12 * S, Fg, TextAnchor.UpperLeft);
            rule.horizontalOverflow = HorizontalWrapMode.Wrap;
            rule.lineSpacing = 1.4f;
            Inset(rule, 14 * S, 12 * S, 14 * S, 8 * S);
            y += 84 * S + 18 * S;

            // ← → 선택 · 취소 · 워프
            var (lk, lw) = KeyHint(body, "←", 24 * S, y + 13 * S);
            var (rk, rw) = KeyHint(body, "→", 24 * S + lw + 4 * S, y + 13 * S);
            Text pick = Label(body, "선택", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(pick, 24 * S + lw + 4 * S + rw + 6 * S, y + 14 * S, 60 * S, 16 * S);
            PrimaryButton(body, sel != null ? $"▶ {sel.Name}(으)로 워프" : "워프", 12 * S, ConfirmWarp, out RectTransform go);
            AtRight(go, 24 * S, y, 220 * S, 44 * S);
            GhostButton(body, "취소  <color=#eaf2ff>[Esc]</color>", 12 * S, CloseWarp, out RectTransform cancel);
            AtRight(cancel, 24 * S + 220 * S + 10 * S, y, 120 * S, 44 * S);
        }
    }
}
