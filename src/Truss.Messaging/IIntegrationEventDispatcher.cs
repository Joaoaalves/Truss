namespace Truss.Messaging
{
    /// <summary>
    /// Delivers a received envelope to the integration event handlers registered for its event type.
    /// Transports call this after receiving a message from the broker.
    /// </summary>
    public interface IIntegrationEventDispatcher
    {
        /// <summary>
        /// Deserializes the envelope and invokes every registered handler for the event
        /// inside a dedicated dependency injection scope.
        /// When a unit of work is registered in the scope, it is committed after the handlers succeed.
        /// </summary>
        /// <param name="envelope">The envelope to dispatch.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Dispatch(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default);
    }
}
