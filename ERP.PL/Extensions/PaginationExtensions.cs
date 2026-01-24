using ERP.BLL.Common;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Extensions
{
    /// <summary>
    /// Extension methods for easy pagination
    /// </summary>
    public static class PaginationExtensions
    {

        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> query,
            int pageNumber,
            int pageSize)
        {
            var count = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>(items, count, pageNumber, pageSize);
        }
    }
}

