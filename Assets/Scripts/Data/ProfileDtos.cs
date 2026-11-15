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
        /// <summary>USER / ADMIN</summary>
        public string Role { get; set; }
    }

    /// <summary>다른 사용자를 표시할 때 쓰는 공통 객체</summary>
    public class UserSummary
    {
        public long UserId { get; set; }
        public string Nickname { get; set; }
        public long? EquippedSkinId { get; set; }
    }
}
