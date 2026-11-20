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
        /// <summary>레이어 안에서의 순서</summary>
        public int SortOrder { get; set; }
        /// <summary>소속 레이어 (스토리 대주제)</summary>
        public LayerSummary Layer { get; set; }
        public List<StageSummary> Stages { get; set; } = new List<StageSummary>();
    }

    /// <summary>레이어 (스토리 대주제). 그 안에 작전(Operation)이 들어간다</summary>
    public class LayerSummary
    {
        public long LayerId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>순서 (튜토리얼 = 0)</summary>
        public int SortOrder { get; set; }
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
        /// <summary>별 3 조건 달성 (데이터 파편 모두 수집 등)</summary>
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
        /// <summary>맵 데이터 JSON. MapData.From()이나 GetMapData&lt;T&gt;()로 바꿔 쓴다</summary>
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
        /// <summary>기록 = 구간 최고 기록의 합 (1~9999). 클리어했을 때만 기록에 반영된다</summary>
        public int? Moves { get; set; }
        /// <summary>불안정 체크포인트(출구 포함) 수. 별 2 판정에 쓴다</summary>
        public int UnstableCount { get; set; }
        /// <summary>워프한 횟수 (통계용)</summary>
        public int Rewinds { get; set; }
        /// <summary>별 2 조건 달성 (조건은 맵의 stars.star2, 게임이 판정). 클리어했을 때만 반영된다</summary>
        public bool ParAchieved { get; set; }
        /// <summary>별 3 조건 달성 (조건은 맵의 stars.star3, 게임이 판정). 클리어했을 때만 반영된다</summary>
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
}
