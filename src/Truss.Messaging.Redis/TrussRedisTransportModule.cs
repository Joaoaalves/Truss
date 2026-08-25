using Truss.Messaging;
using Truss.Messaging.Redis;
using Truss.Messaging.Transport;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the Redis transport.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussRedisTransportModule
    {
        /// <summary>
        /// Registers the Redis transport and its consumer.
        /// Options can be configured here or bound from configuration, for example
        /// services.Configure with the "Truss:Messaging:Redis" section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the transport.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussRedisTransport(
            this IServiceCollection services,
            Action<TrussRedisTransportOptions>? configure = null)
        {
            services.AddOptions<TrussRedisTransportOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddSingleton<RedisTransport>();
            services.AddSingleton<IMessageTransport>(provider => provider.GetRequiredService<RedisTransport>());
            services.AddHostedService<RedisConsumer>();

            return services;
        }
    }
}
