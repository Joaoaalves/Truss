using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Truss.Jobs.Storage;

namespace Truss.Jobs.Runtime
{
    /// <summary>
    /// Reports job runtime health: unreachable storage is unhealthy and
    /// permanently failed jobs degrade the application until inspected.
    /// </summary>
    public sealed class JobsHealthCheck(IJobStore store) : IHealthCheck
    {
        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var statistics = await store.GetStatistics(cancellationToken);

                var data = new Dictionary<string, object>
                {
                    ["queued"] = statistics.QueuedCount,
                    ["running"] = statistics.RunningCount,
                    ["failed"] = statistics.FailedCount
                };

                return statistics.FailedCount > 0
                    ? HealthCheckResult.Degraded($"{statistics.FailedCount} job(s) failed permanently.", data: data)
                    : HealthCheckResult.Healthy("The job runtime is flowing.", data);
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy("The job store is unreachable.", exception);
            }
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Truss.Jobs;
    using Truss.Jobs.Runtime;

    /// <summary>
    /// Provides the job runtime health check registration.
    /// </summary>
    public static class TrussJobsHealthModule
    {
        /// <summary>
        /// Adds the job runtime health check. Requires the jobs module to be
        /// registered with AddTrussJobs.
        /// </summary>
        /// <param name="builder">The health checks builder.</param>
        /// <returns>The updated <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddTrussJobs(this IHealthChecksBuilder builder)
        {
            return builder.Add(new HealthCheckRegistration(
                "truss-jobs",
                provider => new JobsHealthCheck(provider.GetRequiredService<IJobStore>()),
                failureStatus: null,
                tags: null));
        }
    }
}
