using Truss.Messaging;
using Truss.Messaging.Postgres;
using Truss.Messaging.Transport;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the Postgres transport.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussPostgresTransportModule
    {
        /// <summary>
        /// Registers the Postgres transport and its consumer.
        /// Options can be configured here or bound from configuration, for example
        /// services.Configure with the "Truss:Messaging:Postgres" section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the transport.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussPostgresTransport(
            this IServiceCollection services,
            Action<TrussPostgresTransportOptions>? configure = null)
        {
            services.AddOptions<TrussPostgresTransportOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddSingleton<PostgresTransport>();
            services.AddSingleton<IMessageTransport>(provider => provider.GetRequiredService<PostgresTransport>());
            services.AddHostedService<PostgresConsumer>();

            return services;
        }
    }
}
