namespace Executor.Data
{
    /// <summary>GET /profiles/me</summary>
    public class MyProfileResponse
    {
        public long UserId { get; set; }
        public string Email { get; set; }
        public string Nickname { get; set; }
        /// <summary>튜토리얼에서 입력한 집행자 이름. 아직 안 정했으면 null</summary>
        public string CharacterName { get; set; }
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
