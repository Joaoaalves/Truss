using Microsoft.EntityFrameworkCore;
using Truss.Messaging;
using Truss.Messaging.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the EF Core inbox.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussInboxModule
    {
        /// <summary>
        /// Registers the EF Core inbox for the given context: the store the
        /// dispatcher consults before handling a message, and the sweep that
        /// deletes records past their retention. Call after AddTrussMessaging,
        /// and add the inbox table to the context model with
        /// modelBuilder.ApplyTrussInbox().
        /// </summary>
        /// <typeparam name="TDbContext">The context that owns the inbox table.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the retention.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussInbox<TDbContext>(
            this IServiceCollection services,
            Action<TrussInboxOptions>? configure = null)
            where TDbContext : DbContext
        {
            services.AddOptions<TrussInboxOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddScoped<IInboxStore, EfInboxStore<TDbContext>>();
            services.AddHostedService<InboxCleaner>();

            return services;
        }
    }
}
