namespace Truss.Messaging
{
    /// <summary>
    /// Represents an event that crosses the boundary of the application or module that produced it.
    /// Integration events are published after the local transaction commits and are delivered
    /// with at-least-once semantics; handlers must be idempotent.
    /// </summary>
    public interface IIntegrationEvent
    {
        /// <summary>
        /// Gets the unique identifier of this event instance.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the timestamp indicating when the event occurred.
        /// </summary>
        DateTimeOffset OccurredOn { get; }
    }
}
