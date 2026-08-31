using System.Text.Json;
using Truss.Support;
using Xunit;

namespace Truss.Support.Tests
{
    public class SupportWebhookTests
    {
        private static readonly SupportWebhookEvent Event = new(
            Guid.NewGuid(), "deck.support.agent-replied", Guid.NewGuid(), "user-42", "The export is broken",
            new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));

        private static readonly string Body = JsonSerializer.Serialize(
            Event, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        [Fact]
        public void ASignedBody_ParsesBackToTheEvent()
        {
            var signature = SupportWebhook.Sign(Body, "whsec_test");

            Assert.True(SupportWebhook.TryParse(Body, signature, "whsec_test", out var parsed));
            Assert.Equal(Event, parsed);
        }

        [Fact]
        public void ATamperedBody_ParsesToNothing()
        {
            var signature = SupportWebhook.Sign(Body, "whsec_test");

            Assert.False(SupportWebhook.TryParse(Body.Replace("user-42", "user-666"), signature, "whsec_test", out _));
        }

        [Fact]
        public void TheWrongSecret_ParsesToNothing()
        {
            var signature = SupportWebhook.Sign(Body, "whsec_other");

            Assert.False(SupportWebhook.TryParse(Body, signature, "whsec_test", out _));
        }

        [Fact]
        public void AMissingSignature_ParsesToNothing()
        {
            Assert.False(SupportWebhook.TryParse(Body, null, "whsec_test", out _));
            Assert.False(SupportWebhook.TryParse(Body, "", "whsec_test", out _));
        }
    }
}
