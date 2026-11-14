using System;
using Newtonsoft.Json.Linq;

namespace Executor.Data
{
    /// <summary>POST /custom-stages, PUT /custom-stages/{stageId} 요청</summary>
    public class CustomStageRequest
    {
        /// <summary>1~100자</summary>
        public string Title { get; set; }
        /// <summary>1~9999</summary>
        public int ParMoves { get; set; }
        /// <summary>
        /// 맵 JSON 객체 (최대 256KB). hints를 넣으려면 배열이어야 한다.
        /// C# 객체에서 만들려면 JObject.FromObject(맵객체, Json.Serializer)
        /// </summary>
        public JObject MapData { get; set; }
    }

    /// <summary>저장·수정·공개 전환 응답</summary>
    public class CustomStageState
    {
        public long StageId { get; set; }
        public bool IsPublic { get; set; }
        /// <summary>제작자가 클리어했는지. true여야 공개할 수 있다</summary>
        public bool Verified { get; set; }
    }

    /// <summary>GET /custom-stages/me 항목 (비공개 포함)</summary>
    public class MyCustomStage
    {
        public long StageId { get; set; }
        public string Title { get; set; }
        public int ParMoves { get; set; }
        public bool IsPublic { get; set; }
        public bool Verified { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>GET /custom-stages 항목 (공개된 맵)</summary>
    public class PublicCustomStage
    {
        public long StageId { get; set; }
        public string Title { get; set; }
        public UserSummary Creator { get; set; }
        public int ParMoves { get; set; }
        /// <summary>클리어한 사용자 수 (제작자 포함)</summary>
        public long ClearCount { get; set; }
        public bool MyCleared { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class VisibilityRequest
    {
        public bool IsPublic { get; set; }
    }
}
