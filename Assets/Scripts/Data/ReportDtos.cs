// 신고 (Executor-Server docs/api-spec.md 8장)
using System;

namespace Executor.Data
{
    /// <summary>GET /report-categories 항목</summary>
    public class ReportCategory
    {
        public long CategoryId { get; set; }
        public string Name { get; set; }
        /// <summary>신고 대상(사용자 또는 스테이지)을 꼭 정해야 하는지 (버그 제보·기타는 false)</summary>
        public bool TargetRequired { get; set; }
    }

    /// <summary>POST /reports. 대상은 사용자와 스테이지 중 하나만</summary>
    public class ReportRequest
    {
        public long CategoryId { get; set; }
        public long? TargetUserId { get; set; }
        public long? TargetStageId { get; set; }
        /// <summary>1~255자</summary>
        public string Title { get; set; }
        /// <summary>1~2000자</summary>
        public string Comment { get; set; }
    }

    public class ReportResponse
    {
        public long ReportId { get; set; }
        public string Status { get; set; }
        public DateTime? HandledAt { get; set; }
    }

    /// <summary>GET /reports/me 항목 (최신순)</summary>
    public class MyReport
    {
        public long ReportId { get; set; }
        /// <summary>카테고리 이름</summary>
        public string Category { get; set; }
        public string Title { get; set; }
        /// <summary>RECEIVED(접수), IN_PROGRESS(처리 중), DONE(완료)</summary>
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? HandledAt { get; set; }

        public string StatusLabel => Status == "DONE" ? "처리 완료" : Status == "IN_PROGRESS" ? "처리 중" : "접수됨";
    }
}
