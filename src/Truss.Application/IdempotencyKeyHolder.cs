namespace Truss.Application
{
    /// <summary>
    /// Ambient carrier of the idempotency key of the current request.
    /// The HTTP layer sets it from the Idempotency-Key header and the pipeline
    /// behavior reads it; the value flows with the async context.
    /// </summary>
    public static class IdempotencyKeyHolder
    {
        private static readonly AsyncLocal<string?> Value = new();

        /// <summary>
        /// Gets or sets the idempotency key of the current async flow.
        /// </summary>
        public static string? Current
        {
            get => Value.Value;
            set => Value.Value = value;
        }
    }
}
