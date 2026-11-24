using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Executor.Data;

namespace Executor.Network
{
    /// <summary>
    /// 이벤트 스테이지 순위 (/api/events). 전시용이라 로그인 없이 기록을 올리고 순위를 본다.
    /// score: 턴수 이벤트는 턴(적을수록 위), 타자 이벤트는 친 단어 수(많을수록 위)
    /// </summary>
    public static class EventApi
    {
        public static Task<EventRankingResponse> GetRankingAsync(long stageId, int size = 50) =>
            ApiClient.GetAsync<EventRankingResponse>($"/events/{stageId}/records?size={size}", auth: false);

        /// <exception cref="ApiException">409 DUPLICATE_VALUE(이미 사용 중인 이름), 400 INVALID_INPUT, 404 NOT_FOUND</exception>
        public static Task<EventRecordResponse> SubmitAsync(long stageId, string name, int score) =>
            ApiClient.PostAsync<EventRecordResponse>($"/events/{stageId}/records", new EventRecordRequest { Name = name, Score = score }, auth: false);
    }

    public class EventRecordRequest
    {
        public string Name { get; set; }
        public int Score { get; set; }
    }

    public class EventRecordResponse
    {
        public long RecordId { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
        public int Rank { get; set; }
        public long Total { get; set; }
    }

    public class EventRankingResponse
    {
        public long StageId { get; set; }
        public string Title { get; set; }
        public StageType StageType { get; set; }
        public long Total { get; set; }
        public List<EventRankingEntry> Rankings { get; set; } = new List<EventRankingEntry>();
    }

    public class EventRankingEntry
    {
        public long RecordId { get; set; }
        public int Rank { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
