namespace Truss.Application
{
    /// <summary>
    /// One page of results with the counters a client needs to render a pager.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="Items">The items of this page.</param>
    /// <param name="Page">The one-based page number.</param>
    /// <param name="Size">The requested page size.</param>
    /// <param name="TotalCount">The total number of items across every page.</param>
    public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int Size, int TotalCount)
    {
        /// <summary>
        /// Gets the total number of pages.
        /// </summary>
        public int TotalPages => Size > 0 ? (TotalCount + Size - 1) / Size : 0;

        /// <summary>
        /// Gets whether a page exists before this one.
        /// </summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary>
        /// Gets whether a page exists after this one.
        /// </summary>
        public bool HasNextPage => Page < TotalPages;

        /// <summary>
        /// Projects the items of this page, keeping the counters. Use it to turn a
        /// page of entities into a page of DTOs without rebuilding the record.
        /// </summary>
        /// <typeparam name="TResult">The projected item type.</typeparam>
        /// <param name="selector">The projection applied to each item.</param>
        /// <returns>A page of projected items with the same paging counters.</returns>
        public PageResult<TResult> Map<TResult>(Func<T, TResult> selector)
        {
            ArgumentNullException.ThrowIfNull(selector);

            return new PageResult<TResult>([.. Items.Select(selector)], Page, Size, TotalCount);
        }
    }
}
