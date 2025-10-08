using System;
using System.Collections.Generic;

namespace SubscriptionManager.Models.ViewModels
{
    public class Page<T>
    {
        public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; } // 1-based
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

        public static Page<T> Create(IEnumerable<T> items, int totalCount, int pageIndex, int pageSize)
        {
            return new Page<T>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
    }
}