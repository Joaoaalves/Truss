namespace Truss.Messaging
{
    /// <summary>
    /// The wire representation of an integration event: the stable name and version
    /// that identify the contract, plus the JSON payload.
    /// </summary>
    /// <param name="MessageId">The unique identifier of the event instance.</param>
    /// <param name="Name">The stable wire name of the event.</param>
    /// <param name="Version">The version of the event contract.</param>
    /// <param name="OccurredOn">The timestamp of the event.</param>
    /// <param name="Payload">The JSON payload of the event.</param>
    /// <param name="TraceParent">The W3C traceparent of the operation that published the event, when tracing was active, so the consumer's span joins the same distributed trace.</param>
    public sealed record IntegrationEventEnvelope(
        Guid MessageId,
        string Name,
        int Version,
        DateTimeOffset OccurredOn,
        string Payload,
        string? TraceParent = null);
}
