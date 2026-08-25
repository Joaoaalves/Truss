namespace Truss.Application
{
    /// <summary>
    /// A page selection: the one-based page number and the page size.
    /// Limits belong to the query's validator; the request only carries the numbers.
    /// </summary>
    /// <param name="Page">The one-based page number.</param>
    /// <param name="Size">The number of items per page.</param>
    public readonly record struct PageRequest(int Page, int Size)
    {
        /// <summary>
        /// Gets the number of items before this page.
        /// </summary>
        public int Skip => (Page - 1) * Size;
    }
}
