using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>도감 (/api/guidebooks). 로그인 필요</summary>
    public static class GuidebookApi
    {
        /// <summary>도감 목록 (해금 여부 포함). category가 null이면 전체</summary>
        public static Task<GuidebookListResponse> GetListAsync(string category = null) =>
            ApiClient.GetAsync<GuidebookListResponse>(category == null ? "/guidebooks" : $"/guidebooks?category={category}");

        /// <exception cref="ApiException">403 FORBIDDEN (아직 해금하지 않은 항목), 404 NOT_FOUND</exception>
        public static Task<GuidebookDetail> GetDetailAsync(long guidebookId) =>
            ApiClient.GetAsync<GuidebookDetail>($"/guidebooks/{guidebookId}");
    }
}
