using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Truss.Application;
using Truss.EntityFrameworkCore;
using Truss.Application.Pipeline;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the Entity Framework Core persistence module.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussEntityFrameworkModule
    {
        /// <summary>
        /// Registers the EF Core unit of work for the given context and the pipeline behavior
        /// that commits it automatically after every successful command.
        /// The context itself must be registered separately with <c>AddDbContext</c>.
        /// The context also answers for plain <see cref="DbContext"/>, so repositories in
        /// extracted bounded contexts depend on the base type and never on the
        /// application's concrete context.
        /// </summary>
        /// <typeparam name="TDbContext">The database context to bind the unit of work to.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussEntityFramework<TDbContext>(this IServiceCollection services)
            where TDbContext : DbContext
        {
            services.AddScoped<IUnitOfWork, EfUnitOfWork<TDbContext>>();
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
            services.TryAddScoped<DbContext>(provider => provider.GetRequiredService<TDbContext>());

            return services;
        }
    }
}
