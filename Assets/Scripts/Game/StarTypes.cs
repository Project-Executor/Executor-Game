using Executor.Data;

namespace Executor.Game
{
    /// <summary>
    /// 별 조건 종류와 화면 문구 (docs/map-data.md 3.4). 관리자 웹 맵 에디터의 기본 문구와 같게 맞춘다.
    /// </summary>
    public static class StarTypes
    {
        /// <summary>별 1: 클리어 (바꿀 수 없음)</summary>
        public const string Clear = "clear";
        /// <summary>불안정 체크포인트(출구 포함)가 Value개 이하</summary>
        public const string Stable = "stable";
        /// <summary>기록(구간 최고 기록의 합)이 Value턴 이하</summary>
        public const string Moves = "moves";
        /// <summary>워프 Value번 이하</summary>
        public const string Rewinds = "rewinds";
        /// <summary>데이터 파편 모두 모으기 (예전 이름 hidden도 받는다)</summary>
        public const string Fragments = "fragments";
        /// <summary>Target 컴퓨터를 Seconds초 안에 복구</summary>
        public const string Hack = "hack";
        /// <summary>이상 개체 모두 만나기</summary>
        public const string Anomalies = "anomalies";

        /// <summary>예전 이름을 지금 이름으로 (hidden -> fragments, 비어 있으면 fragments)</summary>
        public static string Normalize(string type) =>
            string.IsNullOrEmpty(type) || type == "hidden" ? Fragments : type;

        /// <summary>조건으로 만든 기본 문구 (Text가 없을 때)</summary>
        public static string DefaultText(StarCondition c, string computerName = null)
        {
            switch (Normalize(c?.Type))
            {
                case Clear: return "출구에 도착";
                case Stable: return (c.Value ?? 0) == 0 ? "모든 구간을 턴 안에" : $"불안정 {c.Value}개 이하";
                case Moves: return $"{c.Value ?? 0}턴 이하로 클리어";
                case Rewinds: return (c.Value ?? 0) == 0 ? "워프 없이 클리어" : $"워프 {c.Value}번 이하";
                case Fragments: return "데이터 파편 모두 모으기";
                case Hack: return $"{(string.IsNullOrEmpty(computerName) ? "감염된 컴퓨터" : computerName)}을(를) {c.Seconds ?? 0:0.#}초 안에 복구";
                case Anomalies: return "이상 개체 모두 만나기";
                default: return "알 수 없는 조건";
            }
        }

        /// <summary>별 n의 문구 (관리자가 쓴 Text가 있으면 그것)</summary>
        public static string Text(Board board, int star)
        {
            StarCondition c = board.StarConditionOf(star);
            if (!string.IsNullOrWhiteSpace(c.Text)) return c.Text.Trim();
            return DefaultText(c, board.ComputerOf(c)?.Name);
        }

        /// <summary>지금 진행 상황 (괄호 안에 붙일 짧은 말). 없으면 빈 문자열</summary>
        public static string Progress(Board board, int star)
        {
            StarCondition c = board.StarConditionOf(star);
            switch (Normalize(c.Type))
            {
                case Stable: return $"불안정 {board.UnstableCount}";
                case Moves: return board.IsCleared ? $"기록 {board.RecordMoves}턴" : "";
                case Rewinds: return $"워프 {board.Rewinds}";
                case Fragments: return board.HiddenTotal > 0 ? $"{board.HiddenCollected}/{board.HiddenTotal}" : "";
                case Anomalies: return board.AnomalyTotal > 0 ? $"{board.AnomalyMet}/{board.AnomalyTotal}" : "";
                case Hack:
                    BoardObject pc = board.ComputerOf(c);
                    return pc != null && board.BestHackSeconds(pc.Id) is float best ? $"최고 {best:0.0}초" : "";
                default: return "";
            }
        }

        /// <summary>이 맵에서 받을 수 없는 조건일 때의 설명</summary>
        public static string Unavailable(Board board, int star)
        {
            StarCondition c = board.StarConditionOf(star);
            switch (Normalize(c.Type))
            {
                case Fragments: return "이 스테이지에는 데이터 파편이 없습니다";
                case Anomalies: return "이 스테이지에는 이상 개체가 없습니다";
                default: return "조건이 잘못 설정되었습니다 (관리자 웹에서 확인)";
            }
        }
    }
}
