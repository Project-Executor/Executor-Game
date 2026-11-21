namespace Executor.Data
{
    /// <summary>GET /profiles/me</summary>
    public class MyProfileResponse
    {
        public long UserId { get; set; }
        public string Email { get; set; }
        /// <summary>가입 직후(튜토리얼에서 정하기 전)에는 null. 대사의 {nickname}에도 쓰인다</summary>
        public string Nickname { get; set; }
        public string Comment { get; set; }
        public int Coin { get; set; }
        /// <summary>null이면 기본 스킨</summary>
        public long? EquippedSkinId { get; set; }
        /// <summary>null이면 기본 테마</summary>
        public long? EquippedThemeId { get; set; }
        /// <summary>null이면 기본 커서</summary>
        public long? EquippedCursorId { get; set; }
        /// <summary>null이면 꾸미지 않은 델타</summary>
        public long? EquippedDeltaId { get; set; }
        /// <summary>USER / ADMIN</summary>
        public string Role { get; set; }
    }

    /// <summary>GET /profiles/{userId}: 공개 프로필 (클리어 수·별은 공식 스테이지만)</summary>
    public class ProfileDetailResponse
    {
        public long UserId { get; set; }
        public string Nickname { get; set; }
        public string Comment { get; set; }
        public long? EquippedSkinId { get; set; }
        public long ClearedStageCount { get; set; }
        public long TotalStars { get; set; }
        public bool IsFriend { get; set; }
    }

    /// <summary>PUT /profiles/me/equipment 요청·응답. null이면 기본 (꾸미기는 없음)</summary>
    public class EquipmentRequest
    {
        public long? SkinItemId { get; set; }
        public long? ThemeItemId { get; set; }
        public long? CursorItemId { get; set; }
        public long? DeltaItemId { get; set; }
    }

    public class EquipmentResponse
    {
        public long? EquippedSkinId { get; set; }
        public long? EquippedThemeId { get; set; }
        public long? EquippedCursorId { get; set; }
        public long? EquippedDeltaId { get; set; }
    }

    /// <summary>다른 사용자를 표시할 때 쓰는 공통 객체</summary>
    public class UserSummary
    {
        public long UserId { get; set; }
        public string Nickname { get; set; }
        public long? EquippedSkinId { get; set; }
    }
}
