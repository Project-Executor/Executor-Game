using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>
    /// 스테이지 플레이 (/api/operations, /api/stages). 공식 스테이지와 사용자 맵 모두 같은 API로 플레이한다.
    /// </summary>
    public static class StageApi
    {
        /// <summary>공식 작전·스테이지 목록과 내 진행도 (해금, 별, 최고 기록). 작전 순서, 스테이지 번호 순</summary>
        public static Task<List<OperationResponse>> GetOperationsAsync() =>
            ApiClient.GetAsync<List<OperationResponse>>("/operations");

        /// <summary>스테이지 상세와 맵 데이터 (hints 제외)</summary>
        /// <exception cref="ApiException">403 STAGE_LOCKED, 403 FORBIDDEN(남의 비공개 맵), 404 NOT_FOUND</exception>
        public static Task<StageDetail> GetStageAsync(long stageId) =>
            ApiClient.GetAsync<StageDetail>($"/stages/{stageId}");

        /// <summary>
        /// 한 판 결과 제출 (클리어·게임 오버·포기 모두). 같은 결과를 다시 보내도 보상은 한 번만 준다.
        /// 네트워크 오류로 실패하면 결과를 보관해 두었다가 다시 보낼 것.
        /// </summary>
        /// <exception cref="ApiException">400 INVALID_INPUT, 403 STAGE_LOCKED, 404 NOT_FOUND</exception>
        public static Task<StageResultResponse> SubmitResultAsync(long stageId, StageResultRequest request) =>
            ApiClient.PostAsync<StageResultResponse>($"/stages/{stageId}/results", request);

        /// <summary>클리어 결과 제출 편의 메서드</summary>
        public static Task<StageResultResponse> SubmitClearAsync(long stageId, int moves, bool hiddenFound,
            List<long> encounteredGuidebookIds = null) =>
            SubmitResultAsync(stageId, new StageResultRequest
            {
                Cleared = true,
                Moves = moves,
                HiddenFound = hiddenFound,
                EncounteredGuidebookIds = encounteredGuidebookIds ?? new List<long>()
            });

        /// <summary>게임 오버·포기 결과 제출 편의 메서드 (도전 횟수와 만난 도감만 반영된다)</summary>
        public static Task<StageResultResponse> SubmitFailAsync(long stageId, List<long> encounteredGuidebookIds = null) =>
            SubmitResultAsync(stageId, new StageResultRequest
            {
                Cleared = false,
                EncounteredGuidebookIds = encounteredGuidebookIds ?? new List<long>()
            });

        /// <summary>최소 턴 순위. Friends면 나와 친구만</summary>
        /// <param name="size">1~50</param>
        public static Task<RankingResponse> GetRankingsAsync(long stageId, RankingScope scope = RankingScope.All,
            int size = 20) =>
            ApiClient.GetAsync<RankingResponse>(
                $"/stages/{stageId}/rankings?scope={(scope == RankingScope.Friends ? "friends" : "all")}&size={size}");

        /// <summary>
        /// 힌트 하나를 받는다. 처음 보는 힌트면 힌트 아이템 1개를 쓰고(Charged = true),
        /// 이미 본 힌트(StageDetail.ViewedHintIndexes)는 아이템이 0개여도 무료로 다시 받는다.
        /// </summary>
        /// <param name="hintIndex">0 ~ HintCount - 1</param>
        /// <exception cref="ApiException">400 INVALID_INPUT(범위 밖), 409 NOT_ENOUGH_ITEM, 403, 404</exception>
        public static Task<HintResponse> UseHintAsync(long stageId, int hintIndex) =>
            ApiClient.PostAsync<HintResponse>($"/stages/{stageId}/hint", new HintRequest { HintIndex = hintIndex });
    }
}
