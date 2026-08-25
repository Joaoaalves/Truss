namespace Truss.Messaging
{
    /// <summary>
    /// Publishes integration events.
    /// With the outbox configured, publishing stores the event in the same transaction
    /// as the current command; a background processor delivers it after the commit.
    /// </summary>
    public interface IIntegrationEventPublisher
    {
        /// <summary>
        /// Publishes an integration event.
        /// </summary>
        /// <param name="integrationEvent">The event to publish.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Publish(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
    }
}
