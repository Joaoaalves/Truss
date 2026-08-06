using Microsoft.Extensions.Logging;

namespace Truss.Email
{
    /// <summary>
    /// Development sender: prints the message through the logger instead of
    /// delivering it, so reset links and confirmation codes show up right in
    /// the console output.
    /// </summary>
    public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
    {
        /// <inheritdoc />
        public Task Send(EmailMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            logger.LogInformation(
                "Email to {To}: {Subject}{NewLine}{Body}",
                message.To,
                message.Subject,
                Environment.NewLine,
                message.TextBody ?? message.HtmlBody);

            return Task.CompletedTask;
        }
    }
}
