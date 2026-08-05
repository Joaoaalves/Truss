using System.Text.Json;

namespace Truss.Messaging.RabbitMq
{
    internal static class RabbitMqEnvelope
    {
        public static byte[] ToBody(IntegrationEventEnvelope envelope)
        {
            return JsonSerializer.SerializeToUtf8Bytes(envelope);
        }

        public static IntegrationEventEnvelope? FromBody(ReadOnlyMemory<byte> body)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(body.Span);

                if (envelope is null || envelope.MessageId == Guid.Empty
                    || string.IsNullOrEmpty(envelope.Name) || envelope.Payload is null)
                {
                    return null;
                }

                return envelope;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
