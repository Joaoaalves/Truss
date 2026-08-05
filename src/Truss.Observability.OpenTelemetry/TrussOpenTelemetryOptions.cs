namespace Truss.Observability.OpenTelemetry
{
    /// <summary>
    /// Options for the OpenTelemetry bridge.
    /// </summary>
    public sealed class TrussOpenTelemetryOptions
    {
        /// <summary>
        /// Gets or sets the service name reported in every signal.
        /// Defaults to the entry assembly name.
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the OTLP endpoint. Defaults to null, letting the exporter
        /// use its own default and the standard environment variables, for example
        /// OTEL_EXPORTER_OTLP_ENDPOINT and OTEL_EXPORTER_OTLP_PROTOCOL.
        /// </summary>
        public Uri? OtlpEndpoint { get; set; }

        /// <summary>
        /// Gets or sets whether traces are exported. Defaults to true.
        /// </summary>
        public bool EnableTracing { get; set; } = true;

        /// <summary>
        /// Gets or sets whether metrics are exported. Defaults to true.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets whether logs are exported. Defaults to true.
        /// </summary>
        public bool EnableLogging { get; set; } = true;
    }
}
