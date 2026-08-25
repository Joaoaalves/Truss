using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Truss.Messaging;
using Truss.EntityFrameworkCore;
using Truss.EntityFrameworkCore.Messaging;
using Truss.Messaging.Outbox;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the EF Core outbox.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussOutboxModule
    {
        /// <summary>
        /// Registers the EF Core outbox for the given context: the store, the background processor
        /// and a publisher that stores events in the same transaction as the current command.
        /// Call after AddTrussMessaging, and add the outbox table to the context model
        /// with modelBuilder.ApplyTrussOutbox().
        /// </summary>
        /// <typeparam name="TDbContext">The context that owns the outbox table.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the outbox processor.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussOutbox<TDbContext>(
            this IServiceCollection services,
            Action<TrussOutboxOptions>? configure = null)
            where TDbContext : DbContext
        {
            services.AddOptions<TrussOutboxOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddScoped<IOutboxStore, EfOutboxStore<TDbContext>>();
            services.Replace(ServiceDescriptor.Scoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>());

            services.TryAddSingleton<OutboxCommitInterceptor>();
            services.AddSingleton<IDbContextOptionsConfiguration<TDbContext>>(provider =>
                new OutboxInterceptorConfiguration<TDbContext>(provider.GetRequiredService<OutboxCommitInterceptor>()));

            services.AddSingleton<OutboxProcessor>();
            services.AddHostedService(provider => provider.GetRequiredService<OutboxProcessor>());

            return services;
        }
    }
}
