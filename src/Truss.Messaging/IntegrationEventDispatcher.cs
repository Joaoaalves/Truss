using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application;

namespace Truss.Messaging
{
    /// <summary>
    /// Default dispatcher for received integration events.
    /// Each envelope is handled in its own dependency injection scope; when a unit of work
    /// is registered, it commits after all handlers succeed, so handler state changes are atomic per message.
    /// Each dispatch emits a span through the "Truss.Messaging" activity source.
    /// </summary>
    public class IntegrationEventDispatcher(
        IServiceScopeFactory scopeFactory,
        IIntegrationEventSerializer serializer) : IIntegrationEventDispatcher
    {
        private static readonly ActivitySource Source = new("Truss.Messaging");

        private static readonly ConcurrentDictionary<Type, IntegrationEventHandlerWrapper> Wrappers = new();

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly IIntegrationEventSerializer _serializer = serializer;

        /// <inheritdoc />
        public async Task Dispatch(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            using var activity = Source.StartActivity($"consume {envelope.Name}");
            activity?.SetTag("truss.event", envelope.Name);
            activity?.SetTag("truss.event.version", envelope.Version);
            activity?.SetTag("truss.message_id", envelope.MessageId);

            try
            {
                var integrationEvent = _serializer.Deserialize(envelope);

                var wrapper = Wrappers.GetOrAdd(
                    integrationEvent.GetType(),
                    static eventType => (IntegrationEventHandlerWrapper)Activator.CreateInstance(
                        typeof(IntegrationEventHandlerWrapperImpl<>).MakeGenericType(eventType))!
                );

                await using var scope = _scopeFactory.CreateAsyncScope();

                await wrapper.Handle(integrationEvent, scope.ServiceProvider, cancellationToken);

                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();

                if (unitOfWork is not null)
                    await unitOfWork.CommitAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                throw;
            }
        }
    }
}
