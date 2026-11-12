using System.Collections.Generic;
using System.Runtime.Serialization;
using Executor.Network;
using Newtonsoft.Json.Linq;

namespace Executor.Data
{
    public enum StageType
    {
        [EnumMember(Value = "TUTORIAL")] Tutorial,
        [EnumMember(Value = "NORMAL")] Normal,
        [EnumMember(Value = "BOSS")] Boss
    }

    public enum GuidebookCategory
    {
        /// <summary>오류</summary>
        [EnumMember(Value = "BUG")] Bug,
        /// <summary>바이러스</summary>
        [EnumMember(Value = "VIRUS")] Virus,
        /// <summary>개발 용어</summary>
        [EnumMember(Value = "TERM")] Term
    }

    public enum RankingScope
    {
        [EnumMember(Value = "all")] All,
        [EnumMember(Value = "friends")] Friends
    }

    /// <summary>GET /operations 항목: 작전 하나와 그 안의 스테이지들</summary>
    public class OperationResponse
    {
        public long OperationId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public List<StageSummary> Stages { get; set; } = new List<StageSummary>();
    }

    /// <summary>작전 목록 안의 스테이지 요약과 내 진행도</summary>
    public class StageSummary
    {
        public long StageId { get; set; }
        public int? StageNo { get; set; }
        public StageType StageType { get; set; }
        public string Title { get; set; }
        /// <summary>기준 턴 수. 이 이하로 클리어하면 par 별</summary>
        public int ParMoves { get; set; }
        /// <summary>첫 클리어 보상 코인</summary>
        public int RewardCoin { get; set; }
        public bool Unlocked { get; set; }
        public bool Cleared { get; set; }
        public Stars Stars { get; set; }
        /// <summary>클리어한 최소 턴. 미클리어면 null</summary>
        public int? BestMoves { get; set; }
        public int PlayCount { get; set; }
    }

    /// <summary>
    /// 별. 튜토리얼은 clear 하나뿐이라 max 1, 나머지는 clear·par·hidden 세 개라 max 3
    /// </summary>
    public class Stars
    {
        public bool Clear { get; set; }
        /// <summary>최소 턴이 parMoves 이하</summary>
        public bool Par { get; set; }
        /// <summary>숨겨진 버그 처치 / 피스 수집</summary>
        public bool Hidden { get; set; }
        public int Count { get; set; }
        public int Max { get; set; }
    }

    /// <summary>GET /stages/{stageId}: 공식 스테이지와 사용자 맵 공통</summary>
    public class StageDetail
    {
        public long StageId { get; set; }
        /// <summary>사용자 맵은 null</summary>
        public long? OperationId { get; set; }
        /// <summary>사용자 맵은 null</summary>
        public int? StageNo { get; set; }
        public StageType StageType { get; set; }
        public string Title { get; set; }
        /// <summary>공식 스테이지는 null, 사용자 맵은 제작자</summary>
        public UserSummary Creator { get; set; }
        public int ParMoves { get; set; }
        public int RewardCoin { get; set; }
        /// <summary>받을 수 있는 힌트 개수 (힌트 내용은 힌트 API로만 받는다)</summary>
        public int HintCount { get; set; }
        /// <summary>이미 본 힌트 번호 (오름차순). 이 번호는 다시 받아도 아이템이 빠지지 않는다</summary>
        public List<int> ViewedHintIndexes { get; set; } = new List<int>();
        /// <summary>맵 데이터 JSON (hints는 빠져 있음). 구조가 정해지면 GetMapData&lt;T&gt;()로 바꿔 쓴다</summary>
        public JObject MapData { get; set; }
        public MyRecord MyRecord { get; set; }

        public bool IsOfficial => Creator == null;

        /// <summary>mapData를 원하는 C# 클래스로 바꾼다 (필드 이름은 camelCase JSON과 자동으로 맞춘다)</summary>
        public T GetMapData<T>() => Json.Convert<T>(MapData);
    }

    public class MyRecord
    {
        public bool Cleared { get; set; }
        public Stars Stars { get; set; }
        public int? BestMoves { get; set; }
        public int PlayCount { get; set; }
    }

    /// <summary>POST /stages/{stageId}/results 요청. 클리어·게임 오버·포기 모두 한 판마다 보낸다</summary>
    public class StageResultRequest
    {
        public bool Cleared { get; set; }
        /// <summary>1~9999. 클리어했을 때만 기록에 반영된다</summary>
        public int? Moves { get; set; }
        /// <summary>클리어했을 때만 반영된다</summary>
        public bool HiddenFound { get; set; }
        /// <summary>이번 판에서 만난 이상 개체의 도감 ID (최대 50개). 클리어하지 못해도 해금된다</summary>
        public List<long> EncounteredGuidebookIds { get; set; } = new List<long>();
    }

    public class StageResultResponse
    {
        public bool Cleared { get; set; }
        /// <summary>이번 판의 결과</summary>
        public RunResult ThisRun { get; set; }
        /// <summary>지금까지 누적된 별</summary>
        public Stars Stars { get; set; }
        public int? BestMoves { get; set; }
        public bool IsNewBest { get; set; }
        /// <summary>이번이 첫 클리어인지. 첫 클리어일 때만 보상 코인을 준다</summary>
        public bool FirstClear { get; set; }
        /// <summary>이번에 받은 코인 (첫 클리어가 아니면 0)</summary>
        public int RewardCoin { get; set; }
        /// <summary>보상 반영 후 보유 코인</summary>
        public int TotalCoin { get; set; }
        /// <summary>이번 판에서 새로 해금된 도감</summary>
        public List<UnlockedGuidebook> UnlockedGuidebooks { get; set; } = new List<UnlockedGuidebook>();
        /// <summary>이번 클리어로 열린 다음 공식 스테이지. 없으면 null</summary>
        public long? NextStageId { get; set; }
    }

    public class RunResult
    {
        public bool Clear { get; set; }
        public bool Par { get; set; }
        public bool Hidden { get; set; }
    }

    public class UnlockedGuidebook
    {
        public long GuidebookId { get; set; }
        public GuidebookCategory Category { get; set; }
        public string Title { get; set; }
    }

    /// <summary>GET /stages/{stageId}/rankings</summary>
    public class RankingResponse
    {
        /// <summary>최소 턴 오름차순, 같으면 먼저 클리어한 순</summary>
        public List<RankingEntry> Rankings { get; set; } = new List<RankingEntry>();
        /// <summary>내 순위. 클리어하지 않았으면 null</summary>
        public MyRank Me { get; set; }
    }

    public class RankingEntry
    {
        public int Rank { get; set; }
        public UserSummary User { get; set; }
        public int BestMoves { get; set; }
    }

    public class MyRank
    {
        public long Rank { get; set; }
        public int BestMoves { get; set; }
    }

    public class HintRequest
    {
        public int HintIndex { get; set; }
    }

    /// <summary>POST /stages/{stageId}/hint</summary>
    public class HintResponse
    {
        public int HintIndex { get; set; }
        /// <summary>map_data.hints[hintIndex] 그대로. 보통 문자열이지만 객체일 수도 있다</summary>
        public JToken Content { get; set; }
        /// <summary>이번 요청으로 힌트 아이템을 썼는지. 이미 본 힌트면 false</summary>
        public bool Charged { get; set; }
        /// <summary>남은 힌트 아이템 수 (여러 종류면 합계)</summary>
        public long RemainingHintItems { get; set; }

        /// <summary>힌트가 문자열이면 그 값, 객체면 null</summary>
        public string ContentText => Content?.Type == JTokenType.String ? Content.Value<string>() : null;

        /// <summary>객체 힌트를 원하는 C# 클래스로 바꾼다</summary>
        public T GetContent<T>() => Json.Convert<T>(Content);
    }
}
