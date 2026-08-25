using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Truss.EntityFrameworkCore
{
    /// <summary>
    /// Reports whether the application database answers a connection attempt.
    /// </summary>
    /// <typeparam name="TDbContext">The application context.</typeparam>
    public sealed class DatabaseHealthCheck<TDbContext>(TDbContext context) : IHealthCheck
        where TDbContext : DbContext
    {
        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthCheckContext, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy("The database answers.")
                    : HealthCheckResult.Unhealthy("The database does not answer.");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy("The database does not answer.", exception);
            }
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Truss.EntityFrameworkCore;

    /// <summary>
    /// Provides the database health check registration.
    /// </summary>
    public static class TrussDatabaseHealthModule
    {
        /// <summary>
        /// Adds a connectivity health check for the given context.
        /// </summary>
        /// <typeparam name="TDbContext">The application context.</typeparam>
        /// <param name="builder">The health checks builder.</param>
        /// <returns>The updated <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddTrussDatabase<TDbContext>(this IHealthChecksBuilder builder)
            where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        {
            return builder.Add(new HealthCheckRegistration(
                "truss-database",
                provider => new DatabaseHealthCheck<TDbContext>(provider.GetRequiredService<TDbContext>()),
                failureStatus: null,
                tags: null));
        }
    }
}
