using System;
using System.Collections.Generic;
using System.Linq;

namespace MyDmsVn.Contracts
{
    public sealed class PagedResult<T>
    {
        public PagedResult(
            IEnumerable<T> items,
            int pageNumber,
            int pageSize,
            long totalCount)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (pageNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            if (pageSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            if (totalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCount));
            }

            Items = items.ToArray();
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalCount = totalCount;
        }

        public IReadOnlyList<T> Items { get; }

        public int PageNumber { get; }

        public int PageSize { get; }

        public long TotalCount { get; }
    }
}
