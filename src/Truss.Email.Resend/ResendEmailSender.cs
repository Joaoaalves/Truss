using global::Resend;
using Microsoft.Extensions.Options;

namespace Truss.Email.Resend
{
    /// <summary>
    /// Sender over the official Resend API client. Delivery is one HTTPS call;
    /// rate limits and retries follow the Resend account.
    /// </summary>
    public sealed class ResendEmailSender(IResend resend, IOptions<TrussResendOptions> options) : IEmailSender
    {
        private readonly TrussResendOptions _options = options.Value;

        /// <inheritdoc />
        public async Task Send(EmailMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.From))
            {
                throw new InvalidOperationException(
                    "The Resend sender requires ApiKey and From. Set them in code or bind the Truss:Email:Resend section from configuration."
                );
            }

            await resend.EmailSendAsync(BuildMessage(message, _options), cancellationToken);
        }

        internal static global::Resend.EmailMessage BuildMessage(EmailMessage message, TrussResendOptions options)
        {
            var resendMessage = new global::Resend.EmailMessage
            {
                From = options.FromName is { } name ? $"{name} <{options.From}>" : options.From,
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                TextBody = message.TextBody
            };

            resendMessage.To.Add(message.To);

            return resendMessage;
        }
    }
}
