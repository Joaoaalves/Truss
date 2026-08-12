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
    /// With an inbox registered, an already processed message is skipped and the
    /// processing record commits with the handler's changes, so a duplicate
    /// delivery never commits its side effects twice.
    /// Each dispatch emits a span through the "Truss.Messaging" activity source.
    /// </summary>
    public class IntegrationEventDispatcher(
        IServiceScopeFactory scopeFactory,
        IIntegrationEventSerializer serializer,
        TimeProvider timeProvider) : IIntegrationEventDispatcher
    {
        private static readonly ActivitySource Source = new("Truss.Messaging");

        private static readonly ConcurrentDictionary<Type, IntegrationEventHandlerWrapper> Wrappers = new();

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly IIntegrationEventSerializer _serializer = serializer;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <inheritdoc />
        public async Task Dispatch(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            // The traceparent carried on the envelope makes this span a child of
            // the operation that published the event, across the transport.
            using var activity = envelope.TraceParent is null
                ? Source.StartActivity($"consume {envelope.Name}", ActivityKind.Consumer)
                : Source.StartActivity($"consume {envelope.Name}", ActivityKind.Consumer, envelope.TraceParent);

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
                var inbox = scope.ServiceProvider.GetService<IInboxStore>();

                if (inbox is not null && await inbox.AlreadyProcessed(envelope.MessageId, cancellationToken))
                {
                    activity?.SetTag("truss.duplicate", true);
                    return;
                }

                await wrapper.Handle(integrationEvent, scope.ServiceProvider, cancellationToken);

                if (inbox is not null)
                    await inbox.MarkProcessed(envelope.MessageId, envelope.Name, _timeProvider.GetUtcNow(), cancellationToken);

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
