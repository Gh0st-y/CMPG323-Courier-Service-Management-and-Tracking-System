using System;
using System.Collections.Generic;

namespace CourierService.Domain.Models
{
    public class PagedResult<T>
    {
        public IList<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    }
}