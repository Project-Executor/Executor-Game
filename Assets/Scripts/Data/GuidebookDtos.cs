using System;
using System.Collections.Generic;

namespace Executor.Data
{
    /// <summary>도감 분류 (서버 GuidebookCategory)</summary>
    public static class GuidebookCategories
    {
        public const string Bug = "BUG";
        public const string Virus = "VIRUS";
        public const string Term = "TERM";

        /// <summary>탭 순서</summary>
        public static readonly string[] All = { Bug, Virus, Term };

        /// <summary>한국어 이름 (관리자 웹과 같음)</summary>
        public static string Label(string category)
        {
            switch (category)
            {
                case Bug: return "오류";
                case Virus: return "바이러스";
                case Term: return "개발 용어";
                default: return category ?? "";
            }
        }
    }

    /// <summary>GET /guidebooks. 분류를 지정했으면 개수도 그 분류 안에서 센다</summary>
    public class GuidebookListResponse
    {
        public long UnlockedCount { get; set; }
        public long TotalCount { get; set; }
        public List<GuidebookItem> Items { get; set; } = new List<GuidebookItem>();
    }

    /// <summary>도감 목록 항목. 잠긴 항목은 분류만 보이고 Title·Image·UnlockedAt은 null</summary>
    public class GuidebookItem
    {
        public long GuidebookId { get; set; }
        public string Category { get; set; }
        public bool Unlocked { get; set; }
        public string Title { get; set; }
        public string Image { get; set; }
        public DateTime? UnlockedAt { get; set; }
    }

    /// <summary>GET /guidebooks/{id} (해금한 항목만, 아니면 403)</summary>
    public class GuidebookDetail
    {
        public long GuidebookId { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Image { get; set; }
        public string Comment { get; set; }
        /// <summary>처음 등장하는 스테이지 (없으면 null)</summary>
        public long? StageId { get; set; }
        public DateTime? UnlockedAt { get; set; }
        /// <summary>언어 (예: Java). 없으면 null</summary>
        public string Language { get; set; }
        /// <summary>코드 예시 (여러 줄). 없으면 null</summary>
        public string Code { get; set; }
        /// <summary>대처 방법 한 줄. 없으면 null</summary>
        public string Remedy { get; set; }
        /// <summary>맵에서의 모습 (예: 지나갈 수 없는 칸). 없으면 null</summary>
        public string MapNote { get; set; }
    }
}
