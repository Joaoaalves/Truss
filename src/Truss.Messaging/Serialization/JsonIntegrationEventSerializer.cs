using System.Text.Json;

namespace Truss.Messaging.Serialization
{
    /// <summary>
    /// Default serializer: System.Text.Json payloads inside a name and version envelope.
    /// </summary>
    public sealed class JsonIntegrationEventSerializer(IntegrationEventTypeRegistry registry) : IIntegrationEventSerializer
    {
        private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Default;

        private readonly IntegrationEventTypeRegistry _registry = registry;

        /// <inheritdoc />
        public IntegrationEventEnvelope Serialize(IIntegrationEvent integrationEvent)
        {
            ArgumentNullException.ThrowIfNull(integrationEvent);

            var eventType = integrationEvent.GetType();
            var descriptor = _registry.DescriptorFor(eventType);
            var payload = JsonSerializer.Serialize(integrationEvent, eventType, Options);

            return new IntegrationEventEnvelope(
                integrationEvent.Id,
                descriptor.Name,
                descriptor.Version,
                integrationEvent.OccurredOn,
                payload);
        }

        /// <inheritdoc />
        public IIntegrationEvent Deserialize(IntegrationEventEnvelope envelope)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            var eventType = _registry.Resolve(envelope.Name, envelope.Version);

            return (IIntegrationEvent)JsonSerializer.Deserialize(envelope.Payload, eventType, Options)!;
        }
    }
}
