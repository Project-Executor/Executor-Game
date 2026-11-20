using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 클리어 결과 (시안 Executor_Export/02_ui_screens/Results). 게임 화면을 어둡게 덮고 가운데 stage_clear.log 창:
    /// CLEAR + 별 / 별 조건별 달성 / 사용 턴·워프·보상 / 최소 턴 순위(서버) / R 다시 하기 · 스테이지 선택 · ▶ 다음 스테이지.
    /// 위에는 해금된 도감 알림
    /// </summary>
    public partial class GameUI
    {
        public class ResultInfo
        {
            /// <summary>제목줄 오른쪽 "1-2"</summary>
            public string Code;
            /// <summary>CLEAR 아래 "1-2 · 드라이브를 밀어라"</summary>
            public string Title;
            public int Stars, MaxStars;
            /// <summary>별 조건 줄: (★ 다음 글, 달성, 오른쪽 상태 "달성" / "0 / 1" / "미달성")</summary>
            public List<(string text, bool met, string state)> StarRows = new List<(string, bool, string)>();
            public int Record;
            /// <summary>"/ 15"로 붙일 기준 턴 (체크포인트가 있는 맵은 null)</summary>
            public int? Par;
            public int Rewinds;
            /// <summary>보상 "+50" (없으면 null)</summary>
            public string Reward;
            /// <summary>최고 기록이면 true (사용 턴 칸에 표시)</summary>
            public bool NewBest;
            /// <summary>해금된 도감 이름 (위쪽 알림)</summary>
            public List<string> Unlocked = new List<string>();
            /// <summary>순위 칸을 보일지 (서버 스테이지 + 로그인)</summary>
            public bool HasRanking;
            public bool HasNext;
            public Action OnRetry, OnSelect, OnNext;
        }

        private const float ResLeft = 340 * S, ResTop = 76 * S, ResWidth = 600 * S, ResBorder = 2 * S, ResPadX = 28 * S;

        private GameObject _result;
        private RectTransform _resultWindow, _resultBanner;
        private ResultInfo _resultInfo;
        private Data.RankingResponse _resultRanking;
        private string _resultRankingMessage;
        private long _resultMe;
        private string _resultMyName;
        private Action _resultOpenRanking;

        public bool IsResultShown => _result != null && _result.activeSelf;

        public void ShowResult(ResultInfo info)
        {
            _resultInfo = info;
            _resultRanking = null;
            _resultRankingMessage = info.HasRanking ? "순위를 불러오는 중…" : null;
            _result.SetActive(true);
            DrawResult();
        }

        /// <summary>순위 칸 채우기 (결과를 저장한 뒤 받은 순위). openAll은 "전체 보기"</summary>
        public void SetResultRanking(Data.RankingResponse res, long myUserId, string myName, Action openAll)
        {
            if (!IsResultShown) return;
            _resultRanking = res;
            _resultRankingMessage = res == null ? "순위를 불러오지 못했습니다." : null;
            _resultMe = myUserId;
            _resultMyName = myName;
            _resultOpenRanking = openAll;
            DrawResult();
        }

        private void BuildResult()
        {
            _result = new GameObject("Result", typeof(RectTransform));
            _result.transform.SetParent(_root, false);
            Inset(_result.transform, 0, 0, 0, 0);
            Image dim = Fill(_result.transform, "Dim", Hex(0x0a0716, 0.82f), raycast: true);
            Inset(dim, 0, 0, 0, 0);
        }

        private void DrawResult()
        {
            if (_resultWindow != null) Destroy(_resultWindow.gameObject);
            if (_resultBanner != null) Destroy(_resultBanner.gameObject);
            ResultInfo info = _resultInfo;
            Transform root = _result.transform;

            // 위쪽: 도감 해금 알림
            if (info.Unlocked.Count > 0)
            {
                _resultBanner = Frame(root, "Unlocked", Hex(0x0c0a18, 0.96f), GuidebookAccent, ResBorder, 4 * S);
                Text t = Label(_resultBanner, $"<color=#c3a6ff><b>도감 해금</b></color> · {string.Join(", ", info.Unlocked)}", 12 * S, Fg, TextAnchor.MiddleCenter);
                float w = t.preferredWidth + 28 * S + ResBorder * 2;
                Inset(t, 0, 0, 0, 0);
                _resultBanner.anchorMin = _resultBanner.anchorMax = new Vector2(0.5f, 1);
                _resultBanner.pivot = new Vector2(0.5f, 1);
                _resultBanner.anchoredPosition = new Vector2(0, -24 * S);
                _resultBanner.sizeDelta = new Vector2(w, 16 * S + 16 * S + ResBorder * 2);
                BorderOnTop(_resultBanner);
            }

            RectTransform win = Frame(root, "stage_clear.log", Hex(0x0c0a18, 0.97f), WindowLine, ResBorder, 6 * S);
            _resultWindow = win;
            float inner = ResWidth - (ResBorder + ResPadX) * 2;
            float x = ResBorder + ResPadX;

            // 제목줄
            Image bar = Fill(win, "TitleBar", TitleBar);
            At(bar, ResBorder, ResBorder, ResWidth - ResBorder * 2, 32 * S);
            Image line = Fill(win, "TitleLine", Cyan);
            At(line, ResBorder, ResBorder + 32 * S, ResWidth - ResBorder * 2, 2 * S);
            Text title = Label(bar.transform, "stage_clear.log", 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            Inset(title, 12 * S, 0, 8 * S, 0);
            Text code = Label(bar.transform, info.Code ?? "", 12 * S, Muted, TextAnchor.MiddleRight, UiKit.Mono);
            Inset(code, 12 * S, 0, 12 * S, 0);
            float y = ResBorder + 34 * S + 20 * S;
            const float gap = 18 * S;

            // CLEAR + 별
            Text process = Label(win, "Process finished with exit code 0", 12 * S, Green, TextAnchor.MiddleLeft, UiKit.Mono);
            At(process, x, y, inner, 16 * S);
            foreach (var (color, dx) in new[] { (Magenta, 3 * S), (Cyan, -3 * S), (Fg, 0f) })
            {
                Text clear = Label(win, "CLEAR", 48 * S, color, TextAnchor.MiddleLeft, bold: true);
                At(clear, x + dx, y + 16 * S + 6 * S, 400 * S, 52 * S);
            }
            Text sub = Label(win, info.Title ?? "", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(sub, x, y + 16 * S + 6 * S + 52 * S + 2 * S, inner, 16 * S);
            float blockH = 16 * S + 6 * S + 52 * S + 2 * S + 16 * S;
            const float star = 56 * S, starGap = 8 * S;
            for (int i = 0; i < info.MaxStars; i++)
            {
                Image s = Icon(win, i < info.Stars ? "desktop/star_on" : "desktop/star_off", star);
                if (s == null) continue;
                float sx = x + inner - (info.MaxStars - i) * star - (info.MaxStars - 1 - i) * starGap;
                At(s, sx, y + blockH - star, star, star);
            }
            y += blockH + gap;

            // 별 조건
            foreach (var (text, met, state) in info.StarRows)
            {
                Text left = Label(win, met ? text : $"<color=#8b98b8>{text}</color>", 12 * S, Fg, TextAnchor.MiddleLeft);
                if (!met) left.text = left.text.Replace("<color=#ffd84a>", "<color=#4b5675>");
                At(left, x, y, inner - 120 * S, 16 * S);
                Text right = Label(win, state, 12 * S, met ? Green : Red, TextAnchor.MiddleRight);
                At(right, x + inner - 120 * S, y, 120 * S, 16 * S);
                y += 16 * S + 6 * S;
            }
            if (info.StarRows.Count > 0) y += gap - 6 * S;

            // 사용 턴 · 워프 · 보상
            const float statH = 10 * S + 16 * S + 4 * S + 28 * S + 10 * S, statGap = 12 * S;
            float statW = (inner - statGap * 2) / 3;
            string turns = info.Par != null ? $"{info.Record} <size={(int)(12 * S)}><color=#8b98b8>/ {info.Par}</color></size>" : info.Record.ToString();
            var stats = new (string label, string value, Color color)[]
            {
                (info.NewBest ? "사용 턴 · <color=#ffd84a>최고 기록</color>" : "사용 턴", turns, Fg),
                ("워프", $"{info.Rewinds}<size={(int)(12 * S)}><color=#8b98b8>회</color></size>", Fg),
                ("보상", info.Reward ?? "—", info.Reward != null ? Yellow : Dim),
            };
            for (int i = 0; i < stats.Length; i++)
            {
                Image box = Fill(win, "Stat" + i, Night);
                At(box, x + i * (statW + statGap), y, statW, statH);
                Text l = Label(box.transform, stats[i].label, 12 * S, Muted, TextAnchor.MiddleLeft);
                At(l, 12 * S, 10 * S, statW - 24 * S, 16 * S);
                Text v = Label(box.transform, stats[i].value, 24 * S, stats[i].color, TextAnchor.LowerLeft, UiKit.Mono);
                At(v, 12 * S, 10 * S + 16 * S + 4 * S, statW - 24 * S, 28 * S);
            }
            y += statH + gap;

            // 최소 턴 순위
            if (info.HasRanking)
            {
                Text head = Label(win, "최소 턴 순위", 12 * S, Fg, TextAnchor.MiddleLeft, bold: true);
                At(head, x, y, inner / 2, 16 * S);
                if (_resultRanking != null)
                {
                    string meText = _resultRanking.Me != null ? $"내 순위 {_resultRanking.Me.Rank}위 · " : "";
                    RectTransform link = Node(win, "All");
                    At(link, x + inner / 2, y - 4 * S, inner / 2, 24 * S);
                    HoverArea(link, "Hover", Hex(0x22d3ee, 0.10f), () => _resultOpenRanking?.Invoke(), out Image area);
                    Inset(area, 0, 0, 0, 0);
                    Text all = Label(link, meText + "<color=#22d3ee>전체 보기 ›</color>", 12 * S, Muted, TextAnchor.MiddleRight);
                    Inset(all, 0, 0, 4 * S, 0);
                }
                y += 16 * S + 6 * S;
                const float rowH = 24 * S;
                if (_resultRanking == null || _resultRanking.Rankings.Count == 0)
                {
                    string message = _resultRanking == null ? _resultRankingMessage : "아직 기록이 없습니다.";
                    Text m = Label(win, message, 12 * S, Muted, TextAnchor.MiddleLeft);
                    At(m, x + 8 * S, y, inner, rowH);
                    y += rowH;
                }
                else
                {
                    foreach (var (rank, name, moves, mine) in RankingRows())
                    {
                        if (mine)
                        {
                            Image hl = Fill(win, "Me", Hex(0x22d3ee, 0.14f));
                            At(hl, x, y, inner, rowH);
                            Image hlBar = Fill(win, "MeBar", Cyan);
                            At(hlBar, x, y, 2 * S, rowH);
                        }
                        Color c = mine ? Fg : Muted;
                        Text r = Label(win, $"#{rank}", 12 * S, c, TextAnchor.MiddleLeft, UiKit.Mono);
                        At(r, x + 8 * S, y, 32 * S, rowH);
                        Text n = Label(win, name, 12 * S, c, TextAnchor.MiddleLeft);
                        At(n, x + 8 * S + 32 * S, y, inner - 140 * S, rowH);
                        Text mv = Label(win, $"{moves}턴", 12 * S, c, TextAnchor.MiddleRight, UiKit.Mono);
                        At(mv, x + inner - 100 * S, y, 100 * S - 8 * S, rowH);
                        y += rowH + 2 * S;
                    }
                }
                y += gap;
            }

            // 버튼: R 다시 하기 / 스테이지 선택 / ▶ 다음 스테이지
            const float buttonH = 48 * S, buttonGap = 10 * S;
            float bx = x;
            GhostButton(win, "<color=#b6f4ff>R</color> 다시 하기", 12 * S, () => info.OnRetry?.Invoke(), out RectTransform retry);
            float retryW = 100 * S;
            At(retry, bx, y, retryW, buttonH);
            bx += retryW + buttonGap;
            if (info.HasNext)
            {
                GhostButton(win, "스테이지 선택", 12 * S, () => info.OnSelect?.Invoke(), out RectTransform select);
                At(select, bx, y, 114 * S, buttonH);
                bx += 114 * S + buttonGap;
                PrimaryButton(win, "▶ 다음 스테이지", 24 * S, () => info.OnNext?.Invoke(), out RectTransform next);
                At(next, bx, y, x + inner - bx, buttonH);
            }
            else
            {
                PrimaryButton(win, "▶ 스테이지 선택", 24 * S, () => info.OnSelect?.Invoke(), out RectTransform select);
                At(select, bx, y, x + inner - bx, buttonH);
            }
            y += buttonH + 24 * S + ResBorder;

            At(win, ResLeft, ResTop, ResWidth, y);
            // 창이 화면보다 길면 위로 올린다
            float bottomLimit = 1080 - 24 * S;
            if (ResTop + y > bottomLimit) win.anchoredPosition = new Vector2(ResLeft, -Mathf.Max(8 * S, bottomLimit - y));
            BorderOnTop(win);
        }

        /// <summary>순위 줄: 내가 4위 안이면 위 4명, 아니면 위 3명 + 내 줄</summary>
        private List<(long rank, string name, int moves, bool mine)> RankingRows()
        {
            var rows = new List<(long, string, int, bool)>();
            var list = _resultRanking.Rankings;
            bool meInTop = list.Take(4).Any(e => e.User?.UserId == _resultMe);
            int take = meInTop || _resultRanking.Me == null ? 4 : 3;
            foreach (var e in list.Take(take))
                rows.Add((e.Rank, e.User?.Nickname ?? "?", e.BestMoves, e.User?.UserId == _resultMe));
            if (!meInTop && _resultRanking.Me != null)
                rows.Add((_resultRanking.Me.Rank, _resultMyName ?? "나", _resultRanking.Me.BestMoves, true));
            return rows;
        }
    }
}
