using global::Resend;
using Truss.Email;
using Truss.Email.Resend;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the Resend sender.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussResendEmailModule
    {
        /// <summary>
        /// Registers the Resend sender over the official API client.
        /// Options can be configured here or bound from configuration, for example
        /// services.Configure with the "Truss:Email:Resend" section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the sender.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussResendEmail(
            this IServiceCollection services,
            Action<TrussResendOptions>? configure = null)
        {
            services.AddOptions<TrussResendOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddOptions<ResendClientOptions>()
                .Configure<Microsoft.Extensions.Options.IOptions<TrussResendOptions>>(
                    (client, truss) => client.ApiToken = truss.Value.ApiKey);

            services.AddHttpClient<ResendClient>();
            services.AddTransient<IResend>(provider => provider.GetRequiredService<ResendClient>());

            // Transient on purpose: the sender rides the typed http client, whose
            // handler rotation a singleton would defeat.
            services.AddTransient<IEmailSender, ResendEmailSender>();

            return services;
        }
    }
}
