namespace Truss.Observability
{
    /// <summary>
    /// Options for the observability module.
    /// </summary>
    public sealed class TrussObservabilityOptions
    {
        /// <summary>
        /// Gets or sets whether every request is logged with a structured scope. Defaults to true.
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets whether spans and metrics are emitted per request. Defaults to true.
        /// </summary>
        public bool EnableTracing { get; set; } = true;
    }
}
