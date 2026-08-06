using Truss.Email;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register email senders.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussEmailModule
    {
        /// <summary>
        /// Registers the SMTP sender.
        /// Options can be configured here or bound from configuration, for example
        /// services.Configure with the "Truss:Email:Smtp" section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the sender.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussSmtpEmail(
            this IServiceCollection services,
            Action<TrussSmtpOptions>? configure = null)
        {
            services.AddOptions<TrussSmtpOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddSingleton<IEmailSender, SmtpEmailSender>();

            return services;
        }

        /// <summary>
        /// Registers the console sender, which prints messages to the log
        /// instead of delivering them. Meant for development.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussConsoleEmail(this IServiceCollection services)
        {
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();

            return services;
        }

        /// <summary>
        /// Registers the address validator: RFC syntax through MimeKit plus a DNS
        /// check that the domain accepts mail. Inject IEmailAddressValidator into
        /// validators or handlers that gate on real addresses.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the validation.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussEmailValidation(
            this IServiceCollection services,
            Action<TrussEmailValidationOptions>? configure = null)
        {
            services.AddOptions<TrussEmailValidationOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddSingleton<IEmailAddressValidator, EmailAddressValidator>();

            return services;
        }
    }
}
