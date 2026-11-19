using System.Collections.Generic;
using System.Threading.Tasks;

namespace Executor.Network
{
    /// <summary>타자 해킹 단어 목록 응답 (GET /word-lists/{id})</summary>
    public class WordListResponse
    {
        public long WordListId { get; set; }
        public string Name { get; set; }
        public List<string> Words { get; set; } = new List<string>();
    }

    /// <summary>타자 해킹 단어 목록 (/api/word-lists). 로그인 없이 받을 수 있어서 오프라인 테스트 중에도 서버에 닿으면 불러온다</summary>
    public static class WordListApi
    {
        /// <exception cref="ApiException">404 NOT_FOUND (없거나 지운 목록)</exception>
        public static Task<WordListResponse> GetAsync(long wordListId) =>
            ApiClient.GetAsync<WordListResponse>($"/word-lists/{wordListId}", auth: false);
    }
}
