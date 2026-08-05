namespace Truss.Domain
{
    /// <summary>
    /// Base record for domain events.
    /// Captures the moment the event occurred at creation time.
    /// </summary>
    public abstract record DomainEvent : IDomainEvent
    {
        /// <inheritdoc />
        public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
    }
}
