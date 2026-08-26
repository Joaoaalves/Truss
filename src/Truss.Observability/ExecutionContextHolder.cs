namespace Truss.Observability
{
    /// <summary>
    /// Ambient storage of the current correlation id.
    /// Set by the correlation middleware at the edge, or by any host at the start
    /// of a logical operation; flows automatically through the async call chain.
    /// </summary>
    public static class ExecutionContextHolder
    {
        private static readonly AsyncLocal<string?> Value = new();

        /// <summary>
        /// Gets or sets the correlation id of the current async flow.
        /// </summary>
        public static string? Current
        {
            get => Value.Value;
            set => Value.Value = value;
        }
    }
}
