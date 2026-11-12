// 서버 API 요청·응답 형식 (Executor-Server docs/api-spec.md 기준).
// C# 속성은 PascalCase로 쓰고, JSON의 camelCase와는 Json.Settings가 맞춰 준다.

namespace Executor.Data
{
    /// <summary>오류 응답 { status, code, message }</summary>
    public class ErrorResponse
    {
        public int Status { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
    }

    public class SignupRequest
    {
        /// <summary>이메일 형식, 최대 255자</summary>
        public string Email { get; set; }
        /// <summary>8~64자</summary>
        public string Password { get; set; }
        /// <summary>2~20자</summary>
        public string Nickname { get; set; }
    }

    public class SignupResponse
    {
        public long UserId { get; set; }
        public string Email { get; set; }
        public string Nickname { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        /// <summary>Access Token 유효기간 (초)</summary>
        public long AccessTokenExpiresIn { get; set; }
        public LoginUser User { get; set; }
    }

    public class LoginUser
    {
        public long UserId { get; set; }
        public string Nickname { get; set; }
        /// <summary>USER / ADMIN</summary>
        public string Role { get; set; }
        /// <summary>false면 튜토리얼의 집행자 이름 입력 단계로 보낸다</summary>
        public bool HasCharacterName { get; set; }
    }

    public class ReissueRequest
    {
        public string RefreshToken { get; set; }
    }

    public class TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public long AccessTokenExpiresIn { get; set; }
    }

    public class EmailCheckResponse
    {
        public bool Available { get; set; }
    }

    public class PasswordRequest
    {
        public string Password { get; set; }
    }
}
