using Truss.Messaging;
using Truss.Messaging.RabbitMq;
using Truss.Messaging.Transport;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the RabbitMQ transport.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussRabbitMqTransportModule
    {
        /// <summary>
        /// Registers the RabbitMQ transport and its consumer.
        /// Options can be configured here or bound from configuration, for example
        /// services.Configure with the "Truss:Messaging:RabbitMq" section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the transport.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussRabbitMqTransport(
            this IServiceCollection services,
            Action<TrussRabbitMqTransportOptions>? configure = null)
        {
            services.AddOptions<TrussRabbitMqTransportOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddSingleton<RabbitMqTransport>();
            services.AddSingleton<IMessageTransport>(provider => provider.GetRequiredService<RabbitMqTransport>());
            services.AddHostedService<RabbitMqConsumer>();

            return services;
        }
    }
}
