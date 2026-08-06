using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Truss.Email;
using Truss.Email.Resend;
using Xunit;

namespace Truss.Email.Tests
{
    public class ResendEmailSenderTests
    {
        [Fact]
        public void BuildMessage_MapsEveryField()
        {
            var options = new TrussResendOptions { ApiKey = "key", From = "noreply@shop.dev", FromName = "Shop" };

            var mapped = ResendEmailSender.BuildMessage(
                new EmailMessage("joao@example.com", "Welcome", "<p>Hi</p>", "Hi"),
                options);

            Assert.Equal("Shop <noreply@shop.dev>", mapped.From.ToString());
            Assert.Equal("Welcome", mapped.Subject);
            Assert.Equal("<p>Hi</p>", mapped.HtmlBody);
            Assert.Equal("Hi", mapped.TextBody);
            Assert.Equal("joao@example.com", Assert.Single(mapped.To).ToString());
        }

        [Fact]
        public void BuildMessage_WithoutDisplayName_UsesTheBareAddress()
        {
            var mapped = ResendEmailSender.BuildMessage(
                new EmailMessage("a@b.co", "s", "<p>b</p>"),
                new TrussResendOptions { From = "noreply@shop.dev" });

            Assert.Equal("noreply@shop.dev", mapped.From.ToString());
            Assert.Null(mapped.TextBody);
        }

        [Fact]
        public async Task Send_WithoutConfiguration_ExplainsWhatIsMissing()
        {
            var sender = new ResendEmailSender(null!, Options.Create(new TrussResendOptions()));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sender.Send(new EmailMessage("a@b.co", "s", "<p>b</p>")));
        }

        [Fact]
        public void Registration_ResolvesTheResendSender()
        {
            var provider = new ServiceCollection()
                .AddTrussResendEmail(options =>
                {
                    options.ApiKey = "key";
                    options.From = "noreply@shop.dev";
                })
                .BuildServiceProvider();

            Assert.IsType<ResendEmailSender>(provider.GetRequiredService<IEmailSender>());
        }
    }
}
