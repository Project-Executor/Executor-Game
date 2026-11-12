using System;

namespace Executor.Network
{
    /// <summary>
    /// API 호출 실패. 서버가 보낸 오류면 Status/Code/Message가 서버 값 그대로다
    /// (예: 409 NOT_ENOUGH_COIN). 서버에 닿지 못했으면 Status는 0, Code는 NETWORK_ERROR.
    /// </summary>
    public class ApiException : Exception
    {
        public const string NetworkError = "NETWORK_ERROR";
        public const string SessionExpiredCode = "SESSION_EXPIRED";

        /// <summary>HTTP 상태 코드. 서버에 닿지 못했으면 0</summary>
        public long Status { get; }

        /// <summary>서버 오류 코드 (docs/api-spec.md 0.3 표). 예: INVALID_INPUT, STAGE_LOCKED</summary>
        public string Code { get; }

        public bool IsNetworkError => Status == 0;

        public ApiException(long status, string code, string message) : base(message)
        {
            Status = status;
            Code = code;
        }

        /// <summary>Refresh Token까지 만료돼 다시 로그인해야 한다</summary>
        public static ApiException SessionExpired() =>
            new ApiException(401, SessionExpiredCode, "로그인이 만료되었습니다. 다시 로그인해 주세요.");

        public override string ToString() => $"[{Status} {Code}] {Message}";
    }
}
