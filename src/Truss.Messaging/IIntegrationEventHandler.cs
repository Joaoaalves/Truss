namespace Truss.Messaging
{
    /// <summary>
    /// Handles an integration event received from the transport.
    /// Each message is handled in its own dependency injection scope with its own unit of work.
    /// Delivery is at-least-once, so handlers must tolerate duplicates.
    /// </summary>
    /// <typeparam name="TEvent">The type of the integration event.</typeparam>
    public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
    {
        /// <summary>
        /// Handles the integration event.
        /// </summary>
        /// <param name="integrationEvent">The integration event to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
    }
}
