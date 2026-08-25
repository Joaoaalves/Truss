using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Truss.Messaging.Outbox
{
    /// <summary>
    /// Options for the outbox health check.
    /// </summary>
    public sealed class TrussOutboxHealthOptions
    {
        /// <summary>
        /// Gets or sets the age of the oldest pending message before the check
        /// reports degraded. Defaults to 5 minutes.
        /// </summary>
        public TimeSpan MaxPendingAge { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Reports outbox health: unreachable storage is unhealthy; dead-lettered
    /// messages or a pending message past its age limit degrade the application.
    /// A stuck outbox is also the earliest visible symptom of a broken broker.
    /// </summary>
    public sealed class OutboxHealthCheck(IOutboxStore store, TimeProvider timeProvider, TrussOutboxHealthOptions options) : IHealthCheck
    {
        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var statistics = await store.GetStatistics(cancellationToken);

                var oldestAge = statistics.OldestPendingOccurredOn is { } oldest
                    ? timeProvider.GetUtcNow() - oldest
                    : (TimeSpan?)null;

                var data = new Dictionary<string, object>
                {
                    ["pending"] = statistics.PendingCount,
                    ["failed"] = statistics.FailedCount,
                    ["oldestPendingSeconds"] = Math.Round(oldestAge?.TotalSeconds ?? 0)
                };

                if (statistics.FailedCount > 0)
                    return HealthCheckResult.Degraded($"{statistics.FailedCount} outbox message(s) are dead-lettered.", data: data);

                if (oldestAge > options.MaxPendingAge)
                    return HealthCheckResult.Degraded($"The oldest pending outbox message is {Math.Round(oldestAge.Value.TotalSeconds)} seconds old.", data: data);

                return HealthCheckResult.Healthy("The outbox is flowing.", data);
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy("The outbox store is unreachable.", exception);
            }
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Truss.Messaging;
    using Truss.Messaging.Outbox;

    /// <summary>
    /// Provides the outbox health check registration.
    /// </summary>
    public static class TrussOutboxHealthModule
    {
        /// <summary>
        /// Adds the outbox health check. Requires the outbox to be registered
        /// with AddTrussOutbox.
        /// </summary>
        /// <param name="builder">The health checks builder.</param>
        /// <param name="configure">Optional configuration of the thresholds.</param>
        /// <returns>The updated <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddTrussOutbox(this IHealthChecksBuilder builder, Action<TrussOutboxHealthOptions>? configure = null)
        {
            var options = new TrussOutboxHealthOptions();
            configure?.Invoke(options);

            return builder.Add(new HealthCheckRegistration(
                "truss-outbox",
                provider => new OutboxHealthCheck(
                    provider.GetRequiredService<IOutboxStore>(),
                    provider.GetService<TimeProvider>() ?? TimeProvider.System,
                    options),
                failureStatus: null,
                tags: null));
        }
    }
}
