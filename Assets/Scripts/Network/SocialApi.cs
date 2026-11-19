using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>친구 (/api/friends, /api/friend-requests)와 사용자 검색. 로그인 필요</summary>
    public static class FriendApi
    {
        /// <summary>친구 목록 (닉네임 순)</summary>
        public static Task<List<FriendResponse>> GetFriendsAsync() => ApiClient.GetAsync<List<FriendResponse>>("/friends");

        /// <summary>친구 끊기 (요청 기록도 지워져 다시 요청할 수 있다)</summary>
        /// <exception cref="ApiException">404 NOT_FOUND (친구가 아님)</exception>
        public static Task DeleteFriendAsync(long userId) => ApiClient.DeleteAsync($"/friends/{userId}");

        /// <summary>친구 요청. 상대가 이미 나에게 요청했으면 바로 친구가 된다 (Status ACCEPTED)</summary>
        /// <exception cref="ApiException">400 INVALID_INPUT(자기 자신), 404, 409 ALREADY_FRIEND, 409 REQUEST_ALREADY_EXISTS</exception>
        public static Task<FriendRequestResult> SendRequestAsync(long receiverId) =>
            ApiClient.PostAsync<FriendRequestResult>("/friend-requests", new SendFriendRequest { ReceiverId = receiverId });

        /// <summary>받은 요청 (대기 중, 최근 순)</summary>
        public static Task<List<FriendRequestItem>> GetReceivedAsync() =>
            ApiClient.GetAsync<List<FriendRequestItem>>("/friend-requests/received");

        /// <summary>보낸 요청 (대기 중, 최근 순)</summary>
        public static Task<List<FriendRequestItem>> GetSentAsync() =>
            ApiClient.GetAsync<List<FriendRequestItem>>("/friend-requests/sent");

        /// <exception cref="ApiException">403, 404, 409 REQUEST_ALREADY_HANDLED</exception>
        public static Task<FriendRequestResult> AcceptAsync(long requestId) =>
            ApiClient.PostAsync<FriendRequestResult>($"/friend-requests/{requestId}/accept");

        /// <exception cref="ApiException">403, 404, 409 REQUEST_ALREADY_HANDLED</exception>
        public static Task<FriendRequestResult> RejectAsync(long requestId) =>
            ApiClient.PostAsync<FriendRequestResult>($"/friend-requests/{requestId}/reject");

        /// <summary>보낸 요청 취소</summary>
        /// <exception cref="ApiException">403, 404, 409 REQUEST_ALREADY_HANDLED</exception>
        public static Task CancelAsync(long requestId) => ApiClient.DeleteAsync($"/friend-requests/{requestId}");

        /// <summary>닉네임에 검색어가 들어간 사용자 (나는 빠짐)</summary>
        public static Task<PageResponse<UserSummary>> SearchUsersAsync(string nickname, int size = 20) =>
            ApiClient.GetAsync<PageResponse<UserSummary>>($"/profiles/search?nickname={System.Uri.EscapeDataString(nickname)}&page=0&size={size}");
    }

    /// <summary>친구 메시지 (/api/messages). 로그인 필요</summary>
    public static class MessageApi
    {
        /// <summary>대화 상대 목록 (마지막 메시지가 최근인 순)</summary>
        public static Task<List<MessageRoom>> GetRoomsAsync() => ApiClient.GetAsync<List<MessageRoom>>("/messages/rooms");

        /// <summary>대화 내용 (최신순). 조회하면 상대가 보낸 메시지가 읽음 처리된다</summary>
        public static Task<PageResponse<MessageItem>> GetMessagesAsync(long partnerId, int size = 30) =>
            ApiClient.GetAsync<PageResponse<MessageItem>>($"/messages/{partnerId}?page=0&size={size}");

        /// <exception cref="ApiException">400 INVALID_INPUT(빈 내용·500자 초과), 409 NOT_FRIEND</exception>
        public static Task<SendMessageResponse> SendAsync(long receiverId, string content) =>
            ApiClient.PostAsync<SendMessageResponse>("/messages", new SendMessageRequest { ReceiverId = receiverId, Content = content });

        public static Task<UnreadCountResponse> GetUnreadCountAsync() =>
            ApiClient.GetAsync<UnreadCountResponse>("/messages/unread-count");
    }
}
