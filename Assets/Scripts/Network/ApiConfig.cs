namespace Executor.Network
{
    /// <summary>서버 주소 설정</summary>
    public static class ApiConfig
    {
        /// <summary>배포 서버 (Railway)</summary>
        public const string ProductionBaseUrl = "https://executor-server-production-032d.up.railway.app/api";

        /// <summary>
        /// 로컬 서버. 쓰려면 Project Settings > Player > Other Settings > Allow downloads over HTTP를
        /// "Allowed in Development Builds"로 바꿔야 한다 (Unity 6은 기본적으로 http를 막는다).
        /// </summary>
        public const string LocalBaseUrl = "http://localhost:8080/api";

        /// <summary>요청을 보낼 주소. 끝에 / 없이 /api까지</summary>
        public static string BaseUrl { get; set; } = ProductionBaseUrl;

        /// <summary>요청 하나의 제한 시간 (초)</summary>
        public static int TimeoutSeconds { get; set; } = 15;
    }
}
