// 공지사항 (GET /api/notices, 로그인 없이 조회). 형식: Executor-Server docs/api-spec.md 8-1
using System;

namespace Executor.Data
{
    /// <summary>공지 목록 항목 (내용 제외)</summary>
    public class NoticeSummary
    {
        public long NoticeId { get; set; }
        /// <summary>NOTICE(공지), UPDATE(업데이트), EVENT(이벤트), MAINTENANCE(점검)</summary>
        public string Category { get; set; }
        public string Title { get; set; }
        /// <summary>상단 고정</summary>
        public bool IsPinned { get; set; }
        public DateTime PublishedAt { get; set; }

        /// <summary>분류의 한국어 이름</summary>
        public string CategoryLabel => NoticeCategories.Label(Category);
    }

    /// <summary>공지 상세</summary>
    public class NoticeDetail
    {
        public long NoticeId { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public bool IsPinned { get; set; }
        public DateTime PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string CategoryLabel => NoticeCategories.Label(Category);
    }

    public static class NoticeCategories
    {
        public static string Label(string category)
        {
            switch (category)
            {
                case "UPDATE": return "업데이트";
                case "EVENT": return "이벤트";
                case "MAINTENANCE": return "점검";
                default: return "공지";
            }
        }
    }
}
