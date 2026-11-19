using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
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
            // 열거형은 [EnumMember(Value = "TUTORIAL")]에 적은 서버 문자열로 주고받는다
            Converters = { new StringEnumConverter() },
        };

        /// <summary>JObject.FromObject / ToObject에 넘길 때 쓰는 같은 설정의 Serializer</summary>
        public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);

        /// <summary>JSON 조각(mapData 등)을 C# 클래스로 바꾼다. null이면 default</summary>
        public static T Convert<T>(JToken token) => token == null ? default : token.ToObject<T>(Serializer);
    }

    /// <summary>
    /// 배열 자리에 항목 하나가 객체로 와도 받아 준다 ({...} -> [{...}]). null이면 빈 목록.
    /// 맵 JSON을 손으로 고치다 대괄호를 빠뜨려도 게임이 멈추지 않게 (예: "guides": { "on": "start", ... })
    /// </summary>
    public class SingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(System.Type objectType) => objectType == typeof(System.Collections.Generic.List<T>);

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            var list = new System.Collections.Generic.List<T>();
            if (token.Type == JTokenType.Array)
            {
                foreach (JToken item in token) list.Add(item.ToObject<T>(serializer));
            }
            else if (token.Type == JTokenType.Object)
            {
                list.Add(token.ToObject<T>(serializer));
            }
            return list;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            throw new System.NotSupportedException();
    }
}
