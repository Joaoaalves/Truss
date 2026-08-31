using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Truss.Support
{
    /// <summary>
    /// One notification from the deck. Notifications are cosmetic by design:
    /// the message lives on the deck, so a missed webhook delays an email,
    /// never loses anything.
    /// </summary>
    public sealed record SupportWebhookEvent(
        Guid EventId,
        string Type,
        Guid TicketId,
        string ExternalUserId,
        string Subject,
        DateTimeOffset OccurredOn);

    /// <summary>
    /// The deck's webhook wire: an HMAC-SHA256 of the raw body under the
    /// app's webhook secret, presented as "sha256=&lt;hex&gt;". Verification is
    /// constant-time; a missing or tampered signature parses to nothing.
    /// </summary>
    public static class SupportWebhook
    {
        public const string SignatureHeader = "X-Deck-Signature";

        public const string EventTypeHeader = "X-Deck-Event";

        public static string Sign(string body, string secret)
        {
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
            return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static bool TryParse(string body, string? signature, string secret, out SupportWebhookEvent? webhookEvent)
        {
            webhookEvent = null;

            if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret))
                return false;

            var expected = Encoding.UTF8.GetBytes(Sign(body, secret));
            var presented = Encoding.UTF8.GetBytes(signature.Trim());

            if (!CryptographicOperations.FixedTimeEquals(expected, presented))
                return false;

            try
            {
                webhookEvent = JsonSerializer.Deserialize<SupportWebhookEvent>(
                    body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return false;
            }

            return webhookEvent is not null;
        }
    }
}
