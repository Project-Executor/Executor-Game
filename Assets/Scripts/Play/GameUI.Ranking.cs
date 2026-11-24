using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 순위 창 (시안 Executor/ranking_ui, ranking.exe). 위에 전체·친구 탭, 1~3위 시상대 카드(가운데 1위가 가장 높음),
    /// 4위부터 목록(순위 · 집행자 · 턴 · 클리어 시각), 아래에 내 기록과 [다시 도전 R] [닫기].
    /// 내 줄·카드는 옅은 하늘색 바탕 + 왼쪽 하늘색 선 + "(나)". 비어 있는 순위 카드는 꺼진 메달 + "아직 없음".
    /// </summary>
    public partial class GameUI
    {
        public class RankingView
        {
            /// <summary>창 제목 "ranking.exe — 1-2 · Stage 2"</summary>
            public string Title;
            /// <summary>"Layer 1 · Interface · Hello World!"</summary>
            public string Subtitle;
            public Data.RankingResponse Response;
            public bool Friends;
            public long MyUserId;
            /// <summary>탭을 누르면 (true = 친구)</summary>
            public Action<bool> OnScope;
            public Action OnRetry;
            /// <summary>친구 탭이 비었을 때 "친구 추가하러 가기"</summary>
            public Action OnAddFriends;
        }

        private RankingView _ranking;

        public bool IsRankingOpen => _ranking != null && _popupRoot.childCount > 0;

        private static readonly Color MedalSilver = Hex(0xc9cfe3), MedalBronze = Hex(0xe0915a);
        private static readonly Color MeFill = Hex(0x22d3ee, 0.14f);

        public void ShowRanking(RankingView view)
        {
            ClosePopups();
            _ranking = view;
            const float width = 720, height = 596;
            WindowFrame(_popupRoot, view.Title, null, 280, 56, width, height, true, CloseRanking,
                out RectTransform body, out GameObject blocker);
            Transform bar = blocker.transform.Find("Window/TitleBar");
            for (int i = 0; i < 2 && bar != null; i++) bar.Find("WindowButton" + i)?.gameObject.SetActive(false);

            Data.RankingResponse res = view.Response ?? new Data.RankingResponse();
            List<Data.RankingEntry> list = res.Rankings ?? new List<Data.RankingEntry>();
            long total = Math.Max(res.Total, list.Count);
            const float pad = 22 * S;
            float w = width * S - WinBorder * 2 - pad * 2;
            float bodyH = height * S - WinBorder * 2 - WinTitleH;
            float y = 18 * S;

            // 머리: 작은 줄 + 제목, 오른쪽에 탭
            Text sub = Label(body, view.Subtitle ?? "", 12 * S, Muted, TextAnchor.MiddleLeft, UiKit.Mono);
            At(sub, pad, y, w * 0.6f, 16 * S);
            Text head = Label(body, "최소 턴 순위", 24 * S, Fg, TextAnchor.MiddleLeft, bold: true);
            At(head, pad, y + 20 * S, w * 0.6f, 28 * S);
            float tabRight = pad;
            foreach (bool friends in new[] { true, false })
            {
                bool active = friends == view.Friends;
                string label = (friends ? "친구" : "전체") + (active && total > 0 ? $" · {total}명" : "");
                float tw = TextWidth(body, label, 12 * S, true) + 40 * S;
                RectTransform tab = Node(body, "Tab:" + label);
                AtRight(tab, tabRight, y + 12 * S, tw, 36 * S);
                Image face = Fill(tab, "Face", active ? Cyan : new Color(0, 0, 0, 0), raycast: !active);
                Inset(face, 0, 0, 0, 0);
                if (!active)
                {
                    bool scope = friends;
                    Clickable(face, () => view.OnScope?.Invoke(scope), new Color(0, 0, 0, 0), Hex(0xeaf2ff, 0.08f));
                    Border(tab, TrackLine, 2 * S);
                }
                Text t = Label(tab, label, 12 * S, active ? Ink : Muted, TextAnchor.MiddleCenter, bold: true);
                Inset(t, 0, 0, 0, 0);
                tabRight += tw + 6 * S;
            }
            y += 48 * S + 14 * S;

            // 시상대: 2위 · 1위 · 3위 (아래쪽을 맞춘다)
            const float podiumH = 132 * S, cardGap = 10 * S;
            float cardW = (w - cardGap * 2) / 3;
            var slots = new[] { (rank: 2, h: 112 * S), (rank: 1, h: 132 * S), (rank: 3, h: 100 * S) };
            for (int i = 0; i < 3; i++)
            {
                var (rank, h) = slots[i];
                Data.RankingEntry e = list.FirstOrDefault(r => r.Rank == rank);
                PodiumCard(body, pad + i * (cardW + cardGap), y + podiumH - h, cardW, h, rank, e, e != null && IsMe(e, view));
            }
            y += podiumH + 14 * S;

            // 아래: 내 기록 + 버튼
            const float footH = 44 * S;
            float footY = bodyH - 16 * S - footH;
            Footer(body, pad, footY, w, footH, res, total, view);

            // 4위부터 목록
            float listH = footY - 14 * S - y;
            Image listBg = Fill(body, "List", Night);
            At(listBg, pad, y, w, listH);
            RankRow(listBg.transform, 0, w, 28 * S, "순위", "집행자", "턴", "클리어", Dim, false, header: true);
            List<Data.RankingEntry> rest = list.Where(r => r.Rank > 3).OrderBy(r => r.Rank).ToList();
            bool alone = list.Count <= 1;
            if (rest.Count == 0 && alone)
            {
                EmptyRanking(listBg.transform, w, listH, view);
                return;
            }
            RectTransform viewport = Node(listBg.transform, "Viewport");
            Inset(viewport, 0, 28 * S, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = Fill(viewport, "Hit", new Color(0, 0, 0, 0), raycast: true);
            Inset(hit, 0, 0, 0, 0);
            RectTransform content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            const float rowH = 36 * S;
            for (int i = 0; i < rest.Count; i++)
            {
                Data.RankingEntry r = rest[i];
                bool me = IsMe(r, view);
                RankRow(content, i * rowH, w, rowH, $"#{r.Rank}", Name(r) + (me ? " <color=#22d3ee>(나)</color>" : ""),
                    $"{r.BestMoves}턴", ClearTime(r.ClearedAt), Muted, me);
            }
            content.sizeDelta = new Vector2(0, rest.Count * rowH);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
        }

        public void CloseRanking()
        {
            _ranking = null;
            ClosePopups();
        }

        /// <summary>순위 창의 [다시 도전] (R 키도 같다)</summary>
        public void RetryFromRanking()
        {
            Action retry = _ranking?.OnRetry;
            CloseRanking();
            retry?.Invoke();
        }

        private static bool IsMe(Data.RankingEntry e, RankingView view) => e.User != null && e.User.UserId == view.MyUserId;

        private static string Name(Data.RankingEntry e) => string.IsNullOrEmpty(e.User?.Nickname) ? "(닉네임 없음)" : e.User.Nickname;

        /// <summary>클리어 시각을 한국 시간(이 PC 시간대)으로 "11/23 21:04"</summary>
        private static string ClearTime(DateTime? utc)
        {
            if (utc == null) return "";
            DateTime t = DateTime.SpecifyKind(utc.Value, utc.Value.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : utc.Value.Kind);
            return t.ToLocalTime().ToString("MM/dd HH:mm");
        }

        private static float TextWidth(Transform parent, string text, float size, bool bold)
        {
            Text probe = Label(parent, text, size, Fg, TextAnchor.MiddleLeft, bold: bold);
            float width = probe.preferredWidth;
            Destroy(probe.gameObject);
            return width;
        }

        private static void PodiumCard(Transform parent, float x, float y, float w, float h, int rank, Data.RankingEntry e, bool me)
        {
            Color medal = rank == 1 ? Cyan : rank == 2 ? MedalSilver : MedalBronze;
            Color moves = rank == 1 ? Yellow : medal;
            RectTransform card = Node(parent, "Podium" + rank);
            At(card, x, y, w, h);
            if (rank == 1 && e != null)
            {
                Image shadow = Fill(card, "Shadow", Ink);
                Inset(shadow, 4 * S, 4 * S, -4 * S, -4 * S);
            }
            // 1위: 검은 바탕 위 하늘색 12% (그림자가 비치지 않게 섞은 색을 칠한다)
            Image face = Fill(card, "Face", rank == 1 && e != null ? Hex(0x0f2a36) : Night);
            Inset(face, 0, 0, 0, 0);
            if (me)
            {
                Image tint = Fill(card, "Me", MeFill);
                Inset(tint, 0, 0, 0, 0);
                Image line = Fill(card, "MeLine", Cyan);
                At(line, 0, 0, 3 * S, h);
            }
            Border(card, e == null ? TrackLine : medal, 2 * S);

            float icon = (rank == 1 ? 36 : 30) * S;
            bool big = rank == 1 && e != null;
            float content = icon + 6 * S + 16 * S + (e == null ? 0 : 6 * S + (big ? 28 * S : 16 * S));
            float top = (h - content) / 2;
            Image m = Icon(card, $"ranking/medal_{rank}", icon);
            if (m != null)
            {
                At(m, (w - icon) / 2, top, icon, icon);
                if (e == null) m.color = TrackLine; // 꺼진 메달
            }
            top += icon + 6 * S;
            if (e == null)
            {
                Text none = Label(card, "아직 없음", 12 * S, Muted, TextAnchor.MiddleCenter);
                At(none, 0, top, w, 16 * S);
                return;
            }
            Text name = Label(card, Name(e) + (me ? " <color=#22d3ee>(나)</color>" : ""), 12 * S, Fg, TextAnchor.MiddleCenter, bold: true);
            At(name, 8 * S, top, w - 16 * S, 16 * S);
            top += 16 * S + 6 * S;
            int size = Mathf.RoundToInt((big ? 24 : 12) * S);
            Text mv = Label(card, $"{e.BestMoves}<size={Mathf.RoundToInt(12 * S)}><color=#8b98b8>턴</color></size>", size, moves,
                TextAnchor.MiddleCenter, UiKit.Mono);
            At(mv, 0, top, w, big ? 28 * S : 16 * S);
        }

        /// <summary>목록 한 줄: 순위 56 · 집행자(나머지) · 턴 80 · 클리어 96 (시안 1280 기준 px)</summary>
        private static void RankRow(Transform parent, float y, float w, float h, string rank, string name, string moves, string time,
            Color rankColor, bool me, bool header = false)
        {
            RectTransform row = Node(parent, header ? "Header" : "Row");
            At(row, 0, y, w, h);
            if (me)
            {
                Image tint = Fill(row, "Me", MeFill);
                Inset(tint, 0, 0, 0, 0);
                Image line = Fill(row, "MeLine", Cyan);
                At(line, 0, 0, 3 * S, h);
            }
            Image sep = Fill(row, "Line", TrackLine);
            At(sep, 0, h - (header ? 2 : 1) * S, w, (header ? 2 : 1) * S);
            const float padX = 12 * S;
            Font font = header ? null : UiKit.Mono;  // 머리줄은 보통 글꼴, 숫자 줄은 고정폭
            Text r = Label(row, rank, 12 * S, rankColor, TextAnchor.MiddleLeft, font);
            At(r, padX, 0, 56 * S, h);
            Text n = Label(row, name, 12 * S, header ? Dim : Fg, TextAnchor.MiddleLeft);
            At(n, padX + 56 * S, 0, w - padX * 2 - 56 * S - 176 * S, h);
            Text m = Label(row, moves, 12 * S, header ? Dim : Fg, TextAnchor.MiddleRight, font);
            At(m, w - padX - 176 * S, 0, 80 * S, h);
            Text t = Label(row, time, 12 * S, Dim, TextAnchor.MiddleRight, font);
            At(t, w - padX - 96 * S, 0, 96 * S, h);
        }

        private static void EmptyRanking(Transform list, float w, float h, RankingView view)
        {
            float top = 28 * S + (h - 28 * S - 124 * S) / 2;
            Text big = Label(list, "아직 경쟁자가 없어요", 24 * S, Fg, TextAnchor.MiddleCenter);
            At(big, 0, top, w, 32 * S);
            string msg = view.Friends ? "친구를 추가하면 같은 스테이지 기록을 여기서 비교할 수 있어요."
                                      : "다른 집행자가 이 스테이지를 클리어하면 여기에 나와요.";
            Text small = Label(list, msg, 12 * S, Muted, TextAnchor.MiddleCenter);
            At(small, 0, top + 40 * S, w, 16 * S);
            if (view.Friends && view.OnAddFriends != null)
            {
                GhostButton(list, "친구 추가하러 가기", 12 * S, view.OnAddFriends, out RectTransform add);
                At(add, (w - 143 * S) / 2, top + 76 * S, 143 * S, 36 * S);
            }
        }

        private void Footer(Transform body, float x, float y, float w, float h, Data.RankingResponse res, long total, RankingView view)
        {
            string mine;
            if (res.Me == null) mine = "<color=#8b98b8>아직 이 스테이지를 클리어하지 않았어요</color>";
            else
            {
                string place = view.Friends ? $"친구 중 {res.Me.Rank}위" : total > 0 ? $"{res.Me.Rank}위 / {total}명" : $"{res.Me.Rank}위";
                mine = $"<color=#8b98b8>내 기록</color> <size={Mathf.RoundToInt(24 * S)}><color=#22d3ee>{res.Me.BestMoves}</color></size>턴 · <b>{place}</b>";
            }
            Text me = Label(body, mine, 12 * S, Fg, TextAnchor.LowerLeft);
            At(me, x, y - 6 * S, w - 280 * S, 30 * S);
            Text rule = Label(body, "같은 턴이면 먼저 클리어한 집행자가 앞에 와요", 12 * S, Dim, TextAnchor.MiddleLeft);
            At(rule, x, y + 24 * S, w - 280 * S, 18 * S);

            PrimaryButton(body, "닫기", 12 * S, CloseRanking, out RectTransform close);
            At(close, x + w - 120 * S, y, 120 * S, h);
            if (view.OnRetry != null)
            {
                GhostButton(body, "다시 도전 <color=#b6f4ff>R</color>", 12 * S, RetryFromRanking, out RectTransform retry);
                At(retry, x + w - 120 * S - 12 * S - 124 * S, y, 124 * S, h);
            }
        }
    }
}
