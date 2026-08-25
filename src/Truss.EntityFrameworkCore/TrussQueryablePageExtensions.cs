using Truss.Application;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Provides pagination over queryables.
    /// Lives in the Microsoft.EntityFrameworkCore namespace so it is available
    /// next to the other query operators without additional usings.
    /// </summary>
    public static class TrussQueryablePageExtensions
    {
        /// <summary>
        /// Materializes one page of the query: one count round trip, one page round trip.
        /// Order the query before paging; an unordered skip has no stable meaning.
        /// </summary>
        /// <typeparam name="T">The type of the items.</typeparam>
        /// <param name="source">The query to page.</param>
        /// <param name="page">The page selection.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The requested page with the total counters.</returns>
        public static async Task<PageResult<T>> ToPageAsync<T>(
            this IQueryable<T> source,
            PageRequest page,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);

            var totalCount = await source.CountAsync(cancellationToken);

            var items = await source
                .Skip(page.Skip)
                .Take(page.Size)
                .ToListAsync(cancellationToken);

            return new PageResult<T>(items, page.Page, page.Size, totalCount);
        }
    }
}
