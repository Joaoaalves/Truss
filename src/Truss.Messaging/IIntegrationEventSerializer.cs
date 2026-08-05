namespace Truss.Messaging
{
    /// <summary>
    /// Converts integration events to and from their wire envelope.
    /// </summary>
    public interface IIntegrationEventSerializer
    {
        /// <summary>
        /// Serializes an integration event into its wire envelope.
        /// </summary>
        /// <param name="integrationEvent">The event to serialize.</param>
        IntegrationEventEnvelope Serialize(IIntegrationEvent integrationEvent);

        /// <summary>
        /// Deserializes an envelope back into the integration event type
        /// registered for its name and version.
        /// </summary>
        /// <param name="envelope">The envelope to deserialize.</param>
        IIntegrationEvent Deserialize(IntegrationEventEnvelope envelope);
    }
}
