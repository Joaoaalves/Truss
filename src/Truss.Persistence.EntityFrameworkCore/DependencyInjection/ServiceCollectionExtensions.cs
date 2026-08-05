using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application.Abstractions.Persistence;
using Truss.Application.Abstractions.Requests;
using Truss.Application.Behaviors;

namespace Truss.Persistence.EntityFrameworkCore.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the Entity Framework Core persistence module.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the EF Core unit of work for the given context and the pipeline behavior
        /// that commits it automatically after every successful command.
        /// The context itself must be registered separately with <c>AddDbContext</c>.
        /// </summary>
        /// <typeparam name="TDbContext">The database context to bind the unit of work to.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussEntityFramework<TDbContext>(this IServiceCollection services)
            where TDbContext : DbContext
        {
            services.AddScoped<IUnitOfWork, EfUnitOfWork<TDbContext>>();
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

            return services;
        }
    }
}
