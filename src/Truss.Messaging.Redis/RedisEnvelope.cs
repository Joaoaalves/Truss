using System.Globalization;
using StackExchange.Redis;
using Truss.Messaging.Serialization;

namespace Truss.Messaging.Redis
{
    internal static class RedisEnvelope
    {
        public static NameValueEntry[] ToFields(IntegrationEventEnvelope envelope)
        {
            NameValueEntry[] fields =
            [
                new NameValueEntry("id", envelope.MessageId.ToString()),
                new NameValueEntry("name", envelope.Name),
                new NameValueEntry("version", envelope.Version),
                new NameValueEntry("occurred", envelope.OccurredOn.ToString("O", CultureInfo.InvariantCulture)),
                new NameValueEntry("payload", envelope.Payload)
            ];

            return envelope.TraceParent is null
                ? fields
                : [.. fields, new NameValueEntry("traceparent", envelope.TraceParent)];
        }

        public static IntegrationEventEnvelope? FromEntry(StreamEntry entry)
        {
            string? id = null, name = null, occurred = null, payload = null, traceParent = null;
            int? version = null;

            foreach (var field in entry.Values)
            {
                switch (field.Name.ToString())
                {
                    case "id": id = field.Value; break;
                    case "name": name = field.Value; break;
                    case "version": version = (int)field.Value; break;
                    case "occurred": occurred = field.Value; break;
                    case "payload": payload = field.Value; break;
                    case "traceparent": traceParent = field.Value; break;
                }
            }

            if (id is null || name is null || version is null || occurred is null || payload is null)
                return null;

            return new IntegrationEventEnvelope(
                Guid.Parse(id),
                name,
                version.Value,
                DateTimeOffset.Parse(occurred, CultureInfo.InvariantCulture),
                payload,
                traceParent);
        }
    }
}
