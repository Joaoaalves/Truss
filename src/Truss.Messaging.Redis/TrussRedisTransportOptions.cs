namespace Truss.Messaging.Redis
{
    /// <summary>
    /// Options for the Redis transport.
    /// Bindable from configuration, for example the "Truss:Messaging:Redis" section or
    /// environment variables such as Truss__Messaging__Redis__ConnectionString.
    /// </summary>
    public sealed class TrussRedisTransportOptions
    {
        /// <summary>
        /// Gets or sets the Redis connection string, for example "localhost:6379". Required.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stream key that carries the messages. Defaults to "truss:messages".
        /// Dead-lettered messages go to the same key with a ":dead" suffix.
        /// </summary>
        public string StreamKey { get; set; } = "truss:messages";

        /// <summary>
        /// Gets or sets the consumer group name. Defaults to "truss".
        /// </summary>
        public string ConsumerGroup { get; set; } = "truss";

        /// <summary>
        /// Gets or sets the consumer name inside the group.
        /// Defaults to the machine name plus a unique suffix per process.
        /// </summary>
        public string? ConsumerName { get; set; }

        /// <summary>
        /// Gets or sets whether this application consumes messages. Defaults to true.
        /// Disable on publisher-only applications.
        /// </summary>
        public bool EnableConsumer { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of messages read per iteration. Defaults to 10.
        /// </summary>
        public int BatchSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets the polling interval used when the stream is empty. Defaults to 1 second.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets how long a message may stay pending before another consumer reclaims it.
        /// Defaults to 1 minute. Also the retry delay of a failed message.
        /// </summary>
        public TimeSpan ReclaimIdleAfter { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the delivery limit before a message is dead-lettered. Defaults to 8.
        /// </summary>
        public int MaxAttempts { get; set; } = 8;

        /// <summary>
        /// Gets or sets the approximate maximum length of the stream. Defaults to 100000.
        /// Older delivered entries are trimmed as new ones arrive.
        /// </summary>
        public int MaxStreamLength { get; set; } = 100_000;
    }
}
