using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Truss.Tenancy;
using Truss.Tenancy.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register tenant isolation.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussTenancyModule
    {
        /// <summary>
        /// Registers the tenant context and the interceptor that stamps inserts,
        /// composing with the context registration without touching it. Mark
        /// entities with IsTenantOwned in their configurations and apply the model
        /// with modelBuilder.ApplyTrussTenancy(this).
        /// </summary>
        /// <typeparam name="TDbContext">The context that owns the tenant data.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussTenancy<TDbContext>(this IServiceCollection services)
            where TDbContext : DbContext
        {
            services.TryAddSingleton<ITenantContext, AmbientTenantContext>();
            services.TryAddSingleton<TenantStampInterceptor>();

            services.AddSingleton<IDbContextOptionsConfiguration<TDbContext>>(
                provider => new TenancyInterceptorConfiguration<TDbContext>(
                    provider.GetRequiredService<TenantStampInterceptor>()));

            return services;
        }
    }
}

namespace Truss.Tenancy.EntityFrameworkCore
{
    internal sealed class TenancyInterceptorConfiguration<TDbContext>(TenantStampInterceptor interceptor)
        : IDbContextOptionsConfiguration<TDbContext>
        where TDbContext : DbContext
    {
        public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
    }
}
