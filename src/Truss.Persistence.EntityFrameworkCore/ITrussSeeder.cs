namespace Truss.Persistence.EntityFrameworkCore
{
    /// <summary>
    /// Seeds development data. Implementations are resolved from a scope, so
    /// constructor injection of the context works as anywhere else; they run in
    /// registration order and should be idempotent, checking before inserting.
    /// </summary>
    public interface ITrussSeeder
    {
        /// <summary>
        /// Inserts the seed data.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Seed(CancellationToken cancellationToken = default);
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Truss.Persistence.EntityFrameworkCore;

    /// <summary>
    /// Provides the seeder registrations and the runner.
    /// </summary>
    public static class TrussSeederModule
    {
        /// <summary>
        /// Registers a seeder. Seeders run in registration order.
        /// </summary>
        /// <typeparam name="TSeeder">The seeder type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussSeeder<TSeeder>(this IServiceCollection services)
            where TSeeder : class, ITrussSeeder
        {
            return services.AddScoped<ITrussSeeder, TSeeder>();
        }

        /// <summary>
        /// Runs every registered seeder inside a fresh scope.
        /// The scaffolded Program calls this in development after the schema is ready.
        /// </summary>
        /// <param name="services">The root service provider.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public static async Task RunTrussSeeders(this IServiceProvider services, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(services);

            await using var scope = services.CreateAsyncScope();

            foreach (var seeder in scope.ServiceProvider.GetServices<ITrussSeeder>())
                await seeder.Seed(cancellationToken);
        }
    }
}
