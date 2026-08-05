namespace Truss.Messaging
{
    /// <summary>
    /// Base record for integration events.
    /// Assigns the event identifier and captures the moment the event occurred at creation time.
    /// </summary>
    public abstract record IntegrationEvent : IIntegrationEvent
    {
        /// <inheritdoc />
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <inheritdoc />
        public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
    }
}
