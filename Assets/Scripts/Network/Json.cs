using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Executor.Network
{
    /// <summary>
    /// 서버와 주고받는 JSON 설정. C# 속성 이름(PascalCase)을 서버 필드 이름(camelCase)으로 바꾼다.
    /// 예: AccessToken &lt;-&gt; "accessToken"
    /// </summary>
    public static class Json
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            // 서버가 새 필드를 추가해도 클라이언트가 깨지지 않게 모르는 필드는 무시한다 (기본값)
            MissingMemberHandling = MissingMemberHandling.Ignore,
            // 서버 시각은 UTC ISO-8601 ("2026-11-12T03:00:00Z")
            DateParseHandling = DateParseHandling.DateTimeOffset,
        };

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
