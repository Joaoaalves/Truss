using DnsClient;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Truss.Email
{
    /// <summary>
    /// Options for email address validation.
    /// Bindable from configuration, for example the "Truss:Email:Validation" section.
    /// </summary>
    public sealed class TrussEmailValidationOptions
    {
        /// <summary>
        /// Gets or sets whether the domain is checked for a mail server through DNS.
        /// Defaults to true; disable for fully offline environments.
        /// </summary>
        public bool VerifyMailServer { get; set; } = true;

        /// <summary>
        /// Gets or sets the time limit of the DNS lookup. Defaults to 5 seconds.
        /// A lookup that cannot complete counts as valid, so a slow resolver
        /// never blocks a registration.
        /// </summary>
        public TimeSpan DnsTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Validates addresses with real machinery instead of a regex: MimeKit parses
    /// the syntax under RFC rules, and DNS answers whether the domain can receive
    /// mail at all (an MX record, or the address record fallback of RFC 5321).
    /// </summary>
    public sealed class EmailAddressValidator : IEmailAddressValidator
    {
        private readonly TrussEmailValidationOptions _options;
        private readonly LookupClient _lookup;

        /// <summary>
        /// Initializes the validator with its options.
        /// </summary>
        /// <param name="options">The validation options.</param>
        public EmailAddressValidator(IOptions<TrussEmailValidationOptions> options)
        {
            _options = options.Value;
            _lookup = new LookupClient(new LookupClientOptions { Timeout = _options.DnsTimeout });
        }

        /// <inheritdoc />
        public async Task<EmailAddressValidation> Validate(string address, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address))
                return EmailAddressValidation.Invalid("The address is empty.");

            if (!MailboxAddress.TryParse(ParserOptions.Default, address, out var mailbox)
                || mailbox.Address != address.Trim())
            {
                return EmailAddressValidation.Invalid("The address is not a valid email address.");
            }

            var domainStart = mailbox.Address.LastIndexOf('@') + 1;

            if (domainStart <= 1 || domainStart >= mailbox.Address.Length)
                return EmailAddressValidation.Invalid("The address has no domain.");

            var domain = mailbox.Address[domainStart..];

            if (!domain.Contains('.'))
                return EmailAddressValidation.Invalid("The address domain is incomplete.");

            if (!_options.VerifyMailServer)
                return EmailAddressValidation.Valid;

            return await VerifyDomainAcceptsMail(domain, cancellationToken);
        }

        private async Task<EmailAddressValidation> VerifyDomainAcceptsMail(string domain, CancellationToken cancellationToken)
        {
            try
            {
                var mx = await _lookup.QueryAsync(domain, QueryType.MX, cancellationToken: cancellationToken);

                if (mx.Answers.MxRecords().Any())
                    return EmailAddressValidation.Valid;

                var a = await _lookup.QueryAsync(domain, QueryType.A, cancellationToken: cancellationToken);

                return a.Answers.ARecords().Any()
                    ? EmailAddressValidation.Valid
                    : EmailAddressValidation.Invalid("The address domain accepts no mail.");
            }
            catch (DnsResponseException)
            {
                // An unreachable resolver must never block a registration;
                // only a conclusive "no mail server" answer rejects.
                return EmailAddressValidation.Valid;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return EmailAddressValidation.Valid;
            }
        }
    }
}
