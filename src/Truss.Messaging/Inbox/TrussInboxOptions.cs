namespace Truss.Messaging.Inbox
{
    /// <summary>
    /// Options for the inbox.
    /// </summary>
    public sealed class TrussInboxOptions
    {
        /// <summary>
        /// Gets or sets how long processed records are kept before the sweep
        /// deletes them. Defaults to 7 days, which comfortably outlives every
        /// redelivery window the runtime produces. Set to null to keep them
        /// forever.
        /// </summary>
        public TimeSpan? RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets or sets how often the sweep runs. Defaults to 1 hour.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
    }
}
