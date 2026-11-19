using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>신고 (/api/report-categories, /api/reports). 로그인 필요</summary>
    public static class ReportApi
    {
        public static Task<List<ReportCategory>> GetCategoriesAsync() =>
            ApiClient.GetAsync<List<ReportCategory>>("/report-categories");

        /// <exception cref="ApiException">400 INVALID_INPUT(대상 규칙·자기 자신·길이), 404 NOT_FOUND(카테고리·대상)</exception>
        public static Task<ReportResponse> CreateAsync(ReportRequest request) =>
            ApiClient.PostAsync<ReportResponse>("/reports", request);

        /// <summary>내 신고 (최신순)</summary>
        public static Task<PageResponse<MyReport>> GetMineAsync(int size = 20) =>
            ApiClient.GetAsync<PageResponse<MyReport>>($"/reports/me?page=0&size={size}");
    }
}
