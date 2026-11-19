// 친구·메시지 (Executor-Server docs/api-spec.md 8장)
using System;

namespace Executor.Data
{
    /// <summary>GET /friends 항목</summary>
    public class FriendResponse
    {
        public UserSummary User { get; set; }
        /// <summary>친구가 된 시각</summary>
        public DateTime Since { get; set; }
    }

    /// <summary>GET /friend-requests/received·sent 항목. received면 User는 보낸 사람, sent면 받는 사람</summary>
    public class FriendRequestItem
    {
        public long RequestId { get; set; }
        public UserSummary User { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SendFriendRequest
    {
        public long ReceiverId { get; set; }
    }

    /// <summary>요청 보내기·수락·거절 결과. Status: PENDING, ACCEPTED, REJECTED</summary>
    public class FriendRequestResult
    {
        public long RequestId { get; set; }
        public string Status { get; set; }
    }

    /// <summary>GET /messages/rooms 항목 (마지막 메시지가 최근인 순)</summary>
    public class MessageRoom
    {
        public UserSummary Partner { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }

    /// <summary>GET /messages/{partnerId} 항목 (최신순으로 온다)</summary>
    public class MessageItem
    {
        public long MessageId { get; set; }
        public long SenderId { get; set; }
        public string Content { get; set; }
        /// <summary>받는 사람이 읽었는지 (내가 보낸 메시지면 상대가 읽었는지)</summary>
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SendMessageRequest
    {
        public long ReceiverId { get; set; }
        /// <summary>1~500자</summary>
        public string Content { get; set; }
    }

    public class SendMessageResponse
    {
        public long MessageId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UnreadCountResponse
    {
        public long Count { get; set; }
    }
}
