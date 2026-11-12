using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Data;
using Newtonsoft.Json.Linq;

namespace Executor.Network
{
    /// <summary>
    /// 사용자 제작 맵 관리 (/api/custom-stages). 플레이(상세·결과·순위·힌트)는 StageApi를 그대로 쓴다.
    /// <list type="bullet">
    ///   <item>저장하면 비공개. 비공개 맵은 제작자만 보고 플레이할 수 있다</item>
    ///   <item>제작자가 클리어해야(verified) 공개할 수 있다</item>
    ///   <item>공개된 맵은 수정할 수 없다. 비공개 맵을 수정하면 기록이 지워져 다시 클리어해야 공개할 수 있다</item>
    ///   <item>보상 코인은 항상 0</item>
    /// </list>
    /// </summary>
    public static class CustomStageApi
    {
        /// <summary>공개된 사용자 맵 (최신순)</summary>
        /// <param name="page">0부터</param>
        /// <param name="size">1~50</param>
        public static Task<PageResponse<PublicCustomStage>> GetPublicAsync(int page = 0, int size = 20) =>
            ApiClient.GetAsync<PageResponse<PublicCustomStage>>($"/custom-stages?page={page}&size={size}");

        /// <summary>내가 만든 맵 (비공개 포함, 최근 수정 순)</summary>
        public static Task<List<MyCustomStage>> GetMineAsync() =>
            ApiClient.GetAsync<List<MyCustomStage>>("/custom-stages/me");

        /// <summary>비공개로 저장한다</summary>
        /// <exception cref="ApiException">400 INVALID_INPUT (제목·턴 수 범위, mapData가 객체가 아님, 256KB 초과)</exception>
        public static Task<CustomStageState> CreateAsync(string title, int parMoves, JObject mapData) =>
            ApiClient.PostAsync<CustomStageState>("/custom-stages",
                new CustomStageRequest { Title = title, ParMoves = parMoves, MapData = mapData });

        /// <summary>비공개 맵 수정. 이 맵의 기록과 힌트 열람 기록이 모두 지워진다</summary>
        /// <exception cref="ApiException">403 FORBIDDEN, 404 NOT_FOUND, 409 PUBLISHED_STAGE (공개 중)</exception>
        public static Task<CustomStageState> UpdateAsync(long stageId, string title, int parMoves, JObject mapData) =>
            ApiClient.PutAsync<CustomStageState>($"/custom-stages/{stageId}",
                new CustomStageRequest { Title = title, ParMoves = parMoves, MapData = mapData });

        /// <summary>공개 / 비공개 전환. 비공개로는 언제든 바꿀 수 있다</summary>
        /// <exception cref="ApiException">403 FORBIDDEN, 404 NOT_FOUND, 409 NOT_VERIFIED (클리어하지 않은 맵 공개)</exception>
        public static Task<CustomStageState> SetVisibilityAsync(long stageId, bool isPublic) =>
            ApiClient.PatchAsync<CustomStageState>($"/custom-stages/{stageId}/visibility",
                new VisibilityRequest { IsPublic = isPublic });

        /// <summary>맵 삭제 (플레이 기록도 함께 삭제)</summary>
        /// <exception cref="ApiException">403 FORBIDDEN, 404 NOT_FOUND</exception>
        public static Task DeleteAsync(long stageId) => ApiClient.DeleteAsync($"/custom-stages/{stageId}");
    }
}
