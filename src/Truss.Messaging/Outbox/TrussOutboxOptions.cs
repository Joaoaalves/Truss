namespace Truss.Messaging.Outbox
{
    /// <summary>
    /// Options for the outbox processor.
    /// Bindable from configuration, for example the "Truss:Outbox" section or
    /// environment variables such as Truss__Outbox__BatchSize.
    /// </summary>
    public sealed class TrussOutboxOptions
    {
        /// <summary>
        /// Gets or sets the interval the processor waits when no messages are due. Defaults to 15 seconds.
        /// Commits that store outbox messages wake the processor immediately through the
        /// <see cref="OutboxSignal"/>, so polling is only the safety net for retries and
        /// for messages written by other instances.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Gets or sets the maximum number of messages fetched per iteration. Defaults to 50.
        /// </summary>
        public int BatchSize { get; set; } = 50;

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

        /// <summary>
        /// Gets or sets how long processed messages are kept before deletion.
        /// Defaults to 3 days. Set to null to keep them forever.
        /// Failed messages are never deleted; they wait for inspection.
        /// </summary>
        public TimeSpan? RetentionPeriod { get; set; } = TimeSpan.FromDays(3);

        /// <summary>
        /// Gets or sets how often the processor sweeps processed messages past
        /// their retention. Defaults to 1 hour.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets how often the processor refreshes the depth gauges of
        /// the "Truss.Messaging" meter from the store. Defaults to 30 seconds.
        /// </summary>
        public TimeSpan StatisticsInterval { get; set; } = TimeSpan.FromSeconds(30);
    }
}
