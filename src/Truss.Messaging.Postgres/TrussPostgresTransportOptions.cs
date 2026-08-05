namespace Truss.Messaging.Postgres
{
    /// <summary>
    /// Options for the Postgres transport.
    /// Bindable from configuration, for example the "Truss:Messaging:Postgres" section or
    /// environment variables such as Truss__Messaging__Postgres__ConnectionString.
    /// </summary>
    public sealed class TrussPostgresTransportOptions
    {
        /// <summary>
        /// Gets or sets the connection string of the database that carries the messages. Required.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the NOTIFY channel used to wake consumers. Defaults to "truss_messages".
        /// Only letters, digits and underscores are allowed.
        /// </summary>
        public string Channel { get; set; } = "truss_messages";

        /// <summary>
        /// Gets or sets whether this application consumes messages. Defaults to true.
        /// Disable on publisher-only applications.
        /// </summary>
        public bool EnableConsumer { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the transport creates its tables automatically. Defaults to true.
        /// </summary>
        public bool AutoCreateSchema { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of messages fetched per iteration. Defaults to 10.
        /// </summary>
        public int BatchSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets the fallback polling interval used when no notification arrives. Defaults to 10 seconds.
        /// Notifications normally wake the consumer immediately; polling covers missed signals.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the attempt limit before a message is dead-lettered. Defaults to 8.
        /// </summary>
        public int MaxAttempts { get; set; } = 8;

        /// <summary>
        /// Gets or sets the base delay of the exponential retry backoff. Defaults to 2 seconds.
        /// </summary>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets or sets the upper bound of the retry backoff. Defaults to 5 minutes.
        /// </summary>
        public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    }
}
