using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Truss.Email
{
    /// <summary>
    /// SMTP sender over MailKit. A connection is opened per send, which keeps the
    /// sender safe under concurrency; the throughput path for bulk mail is a
    /// provider API, not SMTP.
    /// </summary>
    public sealed class SmtpEmailSender(IOptions<TrussSmtpOptions> options) : IEmailSender
    {
        private readonly TrussSmtpOptions _options = options.Value;

        /// <inheritdoc />
        public async Task Send(EmailMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.From))
            {
                throw new InvalidOperationException(
                    "The SMTP sender requires Host and From. Set them in code or bind the Truss:Email:Smtp section from configuration."
                );
            }

            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_options.FromName ?? _options.From, _options.From));
            mime.To.Add(MailboxAddress.Parse(message.To));
            mime.Subject = message.Subject;

            var body = new BodyBuilder { HtmlBody = message.HtmlBody };

            if (message.TextBody is not null)
                body.TextBody = message.TextBody;

            mime.Body = body.ToMessageBody();

            using var client = new SmtpClient();

            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrEmpty(_options.UserName))
                await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, cancellationToken);

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
    }
}
