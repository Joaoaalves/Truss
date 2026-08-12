using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Truss.Jobs;
using Truss.Messaging;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the Truss jobs module.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussJobsModule
    {
        /// <summary>
        /// Registers the job runtime: the scheduler, the monitor, the executor, the scheduled
        /// and recurring job services, and every job found in the configured assemblies.
        /// Requires AddTrussMessaging to be called first; job delivery flows through the
        /// messaging pipeline, so it inherits the outbox transactionality and the transport durability.
        /// An in-memory store is used unless a persistent one is registered.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">The configuration action used to expose job assemblies and settings.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when messaging is not registered or no assembly is given.</exception>
        public static IServiceCollection AddTrussJobs(this IServiceCollection services, Action<TrussJobsOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            if (!services.Any(descriptor => descriptor.ServiceType == typeof(IIntegrationEventSerializer)))
            {
                throw new InvalidOperationException(
                    "AddTrussJobs requires messaging. Call AddTrussMessaging before AddTrussJobs."
                );
            }

            var options = new TrussJobsOptions();
            configure(options);

            if (options.Assemblies.Count == 0)
            {
                throw new InvalidOperationException(
                    "At least one assembly must be registered. Use options.AddAssembly<TMarker>() to expose the assemblies that contain your jobs."
                );
            }

            var registry = JobTypeRegistry.FromAssemblies(options.Assemblies);

            services.AddSingleton(registry);
            services.Configure<TrussJobsOptions>(o =>
            {
                foreach (var assembly in options.Assemblies)
                    o.Assemblies.Add(assembly);

                foreach (var recurring in options.Recurring)
                    o.Recurring.Add(recurring);

                o.MaxAttempts = options.MaxAttempts;
                o.JobTimeout = options.JobTimeout;
                o.RetryBaseDelay = options.RetryBaseDelay;
                o.RetryMaxDelay = options.RetryMaxDelay;
                o.CancellationPollingInterval = options.CancellationPollingInterval;
                o.RetentionPeriod = options.RetentionPeriod;
                o.CleanupInterval = options.CleanupInterval;
                o.SchedulerLockLeaseDuration = options.SchedulerLockLeaseDuration;
                o.ScheduledPollingInterval = options.ScheduledPollingInterval;
                o.RecurringTickInterval = options.RecurringTickInterval;
                o.EnableSchedulers = options.EnableSchedulers;
            });

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(provider => new JobMetrics(provider.GetService<IMeterFactory>()));
            services.AddScoped<IJobScheduler, JobScheduler>();
            services.AddScoped<IJobMonitor, JobMonitor>();

            services.TryAddSingleton<InMemoryJobStore>();
            services.TryAddScoped<IJobStore>(provider => provider.GetRequiredService<InMemoryJobStore>());
            services.TryAddScoped<ISchedulerLock, LocalSchedulerLock>();

            foreach (var jobType in registry.JobTypes)
                services.AddTransient(jobType);

            services.AddTrussMessagingAssembly(typeof(JobEnqueued).Assembly);

            services.AddHostedService<ScheduledJobsPoller>();
            services.AddHostedService<RecurringJobsService>();

            return services;
        }
    }
}
