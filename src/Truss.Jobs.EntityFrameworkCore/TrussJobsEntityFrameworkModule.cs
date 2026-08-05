using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Truss.Jobs;
using Truss.Jobs.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the EF Core job store.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussJobsEntityFrameworkModule
    {
        /// <summary>
        /// Registers the EF Core job store for the given context, replacing the in-memory store.
        /// Call after AddTrussJobs, and add the job table to the context model
        /// with modelBuilder.ApplyTrussJobs().
        /// </summary>
        /// <typeparam name="TDbContext">The context that owns the job table.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussJobsEntityFramework<TDbContext>(this IServiceCollection services)
            where TDbContext : DbContext
        {
            services.Replace(ServiceDescriptor.Scoped<IJobStore, EfJobStore<TDbContext>>());

            return services;
        }
    }
}
