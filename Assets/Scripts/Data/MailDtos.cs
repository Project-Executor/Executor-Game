// 우편 (GET /api/mails, POST /api/mails/{id}/claim, POST /api/mails/claim-all). 형식: Executor-Server docs/api-spec.md 7장
using System;
using System.Collections.Generic;

namespace Executor.Data
{
    /// <summary>우편 한 통 (만료되지 않은 것만 온다. 받은 우편도 claimed: true로 남는다)</summary>
    public class MailResponse
    {
        public long MailId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int RewardCoin { get; set; }
        /// <summary>첨부 아이템. 없으면 null</summary>
        public MailItem RewardItem { get; set; }
        public int RewardItemQuantity { get; set; }
        public bool Claimed { get; set; }
        /// <summary>만료 시각. null이면 무기한</summary>
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>보상 한 줄 요약: "코인 100 · 다크 모드 집행자 ×1" (없으면 "보상 없음")</summary>
        public string RewardText
        {
            get
            {
                var parts = new List<string>();
                if (RewardCoin > 0) parts.Add($"코인 {RewardCoin}");
                if (RewardItem != null) parts.Add($"{RewardItem.Name} ×{RewardItemQuantity}");
                return parts.Count == 0 ? "보상 없음" : string.Join(" · ", parts);
            }
        }
    }

    public class MailItem
    {
        public long ItemId { get; set; }
        public string Name { get; set; }
        /// <summary>SKIN, THEME</summary>
        public string ItemType { get; set; }
    }

    /// <summary>POST /mails/{id}/claim</summary>
    public class MailClaimResponse
    {
        public long MailId { get; set; }
        public int RewardCoin { get; set; }
        /// <summary>이미 가진 스킨·테마를 코인으로 바꾼 금액. 없으면 0</summary>
        public int ConvertedCoin { get; set; }
        /// <summary>실제로 받은 아이템. 첨부가 없거나 코인으로 바꿨으면 null</summary>
        public ClaimedItem RewardItem { get; set; }
        /// <summary>받은 뒤 내 코인</summary>
        public int TotalCoin { get; set; }
    }

    public class ClaimedItem
    {
        public long ItemId { get; set; }
        public string ItemType { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>POST /mails/claim-all</summary>
    public class MailClaimAllResponse
    {
        public int ClaimedCount { get; set; }
        public int RewardCoin { get; set; }
        public int ConvertedCoin { get; set; }
        public List<ClaimedItem> RewardItems { get; set; } = new List<ClaimedItem>();
        public int TotalCoin { get; set; }
    }
}
