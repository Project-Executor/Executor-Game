using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>우편함 (/api/mails). 로그인 필요</summary>
    public static class MailApi
    {
        /// <summary>만료되지 않은 우편 (최신순, size 1~50). 받은 우편도 claimed: true로 온다</summary>
        public static Task<PageResponse<MailResponse>> GetMailsAsync(int page = 0, int size = 50) =>
            ApiClient.GetAsync<PageResponse<MailResponse>>($"/mails?page={page}&size={size}");

        /// <summary>우편 하나 받기</summary>
        /// <exception cref="ApiException">404 NOT_FOUND, 409 ALREADY_CLAIMED, 409 MAIL_EXPIRED</exception>
        public static Task<MailClaimResponse> ClaimAsync(long mailId) =>
            ApiClient.PostAsync<MailClaimResponse>($"/mails/{mailId}/claim");

        /// <summary>받을 수 있는 우편을 모두 받기 (없으면 claimedCount 0)</summary>
        public static Task<MailClaimAllResponse> ClaimAllAsync() =>
            ApiClient.PostAsync<MailClaimAllResponse>("/mails/claim-all");
    }
}
