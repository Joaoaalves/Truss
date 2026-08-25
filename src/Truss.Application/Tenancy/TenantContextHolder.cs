namespace Truss.Tenancy
{
    /// <summary>
    /// Ambient carrier of the tenant of the current request.
    /// The HTTP layer resolves it, the persistence layer filters and stamps by it,
    /// and the value flows with the async context.
    /// </summary>
    public static class TenantContextHolder
    {
        private static readonly AsyncLocal<Guid?> Value = new();

        /// <summary>
        /// Gets or sets the tenant of the current async flow.
        /// </summary>
        public static Guid? Current
        {
            get => Value.Value;
            set => Value.Value = value;
        }
    }
}
