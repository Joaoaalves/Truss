using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Truss.Email;
using Xunit;

namespace Truss.Email.Tests
{
    public class ConsoleEmailSenderTests
    {
        private sealed class CapturedLogger<T> : ILogger<T>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception));
            }
        }

        [Fact]
        public async Task ConsoleSender_PrintsTheMessageToTheLog()
        {
            var logger = new CapturedLogger<ConsoleEmailSender>();
            var sender = new ConsoleEmailSender(logger);

            await sender.Send(new EmailMessage("joao@example.com", "Reset your password", "<a>reset</a>", "reset: https://x/reset?t=abc"));

            var entry = Assert.Single(logger.Messages);
            Assert.Contains("joao@example.com", entry);
            Assert.Contains("Reset your password", entry);
            Assert.Contains("https://x/reset?t=abc", entry);
        }

        [Fact]
        public void Registrations_ResolveASender()
        {
            var console = new ServiceCollection().AddLogging().AddTrussConsoleEmail().BuildServiceProvider();
            Assert.IsType<ConsoleEmailSender>(console.GetRequiredService<IEmailSender>());

            var smtp = new ServiceCollection().AddTrussSmtpEmail(options => options.Host = "localhost").BuildServiceProvider();
            Assert.IsType<SmtpEmailSender>(smtp.GetRequiredService<IEmailSender>());
        }

        [Fact]
        public async Task SmtpSender_WithoutHost_ExplainsWhatIsMissing()
        {
            var sender = new SmtpEmailSender(Options.Create(new TrussSmtpOptions()));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sender.Send(new EmailMessage("a@b.c", "s", "<p>b</p>")));
        }
    }

    public class SmtpEmailSenderTests
    {
        private const string MailpitSmtpHost = "localhost";
        private const int MailpitSmtpPort = 10250;
        private const string MailpitApi = "http://localhost:10825";

        private static async Task EnsureMailpit()
        {
            using var http = new HttpClient();
            var deadline = DateTime.UtcNow.AddSeconds(120);
            var started = false;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await http.GetStringAsync($"{MailpitApi}/api/v1/info");
                    return;
                }
                catch (HttpRequestException)
                {
                    if (!started)
                    {
                        RunDocker("start truss-test-mailpit");
                        RunDocker("run -d --name truss-test-mailpit -p 10250:1025 -p 10825:8025 axllent/mailpit");
                        started = true;
                    }

                    await Task.Delay(1000);
                }
            }

            Assert.Fail("Container truss-test-mailpit did not become ready. Is docker running?");
        }

        private static void RunDocker(string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("docker", arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                process?.WaitForExit(30_000);
            }
            catch
            {
            }
        }

        [Fact]
        public async Task SmtpSender_DeliversTheMessage()
        {
            await EnsureMailpit();

            var sender = new SmtpEmailSender(Options.Create(new TrussSmtpOptions
            {
                Host = MailpitSmtpHost,
                Port = MailpitSmtpPort,
                From = "noreply@trussshop.dev",
                FromName = "TrussShop",
                UseStartTls = false
            }));

            var marker = Guid.NewGuid().ToString("N");
            await sender.Send(new EmailMessage("joao@example.com", $"Welcome {marker}", $"<p>Hello {marker}</p>", $"Hello {marker}"));

            using var http = new HttpClient();
            var search = await http.GetFromJsonAsync<JsonElement>($"{MailpitApi}/api/v1/search?query={marker}");

            Assert.True(search.GetProperty("messages_count").GetInt32() >= 1);

            var message = search.GetProperty("messages")[0];
            Assert.Equal($"Welcome {marker}", message.GetProperty("Subject").GetString());
            Assert.Equal("joao@example.com", message.GetProperty("To")[0].GetProperty("Address").GetString());
            Assert.Equal("noreply@trussshop.dev", message.GetProperty("From").GetProperty("Address").GetString());
        }
    }
}
