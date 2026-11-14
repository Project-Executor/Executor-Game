using System.Collections.Generic;

namespace Executor.Data
{
    /// <summary>페이지 단위 조회 응답 (page는 0부터, size는 1~50)</summary>
    public class PageResponse<T>
    {
        public List<T> Content { get; set; } = new List<T>();
        public int Page { get; set; }
        public int Size { get; set; }
        public long TotalElements { get; set; }
        public int TotalPages { get; set; }

        public bool HasNext => Page + 1 < TotalPages;
    }
}
