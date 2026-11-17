using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>공지사항 (/api/notices). 로그인 없이 볼 수 있어서 오프라인 테스트 중에도 서버에 닿으면 불러온다</summary>
    public static class NoticeApi
    {
        /// <summary>지금 게시 중인 공지 (상단 고정 먼저, 최신순)</summary>
        public static Task<PageResponse<NoticeSummary>> GetNoticesAsync(int page = 0, int size = 20) =>
            ApiClient.GetAsync<PageResponse<NoticeSummary>>($"/notices?page={page}&size={size}", auth: false);

        /// <summary>공지 상세</summary>
        /// <exception cref="ApiException">404 NOT_FOUND (숨김·예약·종료된 공지)</exception>
        public static Task<NoticeDetail> GetNoticeAsync(long noticeId) =>
            ApiClient.GetAsync<NoticeDetail>($"/notices/{noticeId}", auth: false);
    }
}
