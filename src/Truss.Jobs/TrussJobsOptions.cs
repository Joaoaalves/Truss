using System.Reflection;
using System.Text.Json;

namespace Truss.Jobs
{
    internal sealed record RecurringJobDefinition(string Cron, Type JobType, string ArgsPayload);

    /// <summary>
    /// Options used to configure the Truss jobs module.
    /// Numeric and time settings are bindable from configuration, for example the
    /// "Truss:Jobs" section or environment variables such as Truss__Jobs__MaxAttempts.
    /// </summary>
    public sealed class TrussJobsOptions
    {
        internal List<Assembly> Assemblies { get; } = [];

        internal List<RecurringJobDefinition> Recurring { get; } = [];

        /// <summary>
        /// Gets or sets the attempt limit before a job is failed permanently. Defaults to 3.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the execution time limit per attempt. Defaults to null, meaning no limit.
        /// </summary>
        public TimeSpan? JobTimeout { get; set; }

        /// <summary>
        /// Gets or sets the base delay of the exponential backoff between attempts.
        /// Defaults to 5 seconds. Set to zero to retry immediately.
        /// </summary>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the upper bound of the retry backoff. Defaults to 5 minutes.
        /// </summary>
        public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets how often a running job checks for a cancellation request.
        /// Defaults to 2 seconds.
        /// </summary>
        public TimeSpan CancellationPollingInterval { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets or sets how long succeeded and cancelled jobs are kept before deletion.
        /// Defaults to 7 days. Set to null to keep them forever.
        /// Failed jobs are never deleted; they wait for inspection.
        /// </summary>
        public TimeSpan? RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets or sets how often the scheduler sweeps finished jobs past their retention.
        /// Defaults to 1 hour.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets the lease duration of the scheduler lock. Defaults to 30 seconds.
        /// When the leader stops, another instance takes over once the lease expires.
        /// </summary>
        public TimeSpan SchedulerLockLeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the interval the scheduler polls for due scheduled jobs. Defaults to 5 seconds.
        /// </summary>
        public TimeSpan ScheduledPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the tick interval of the recurring job scheduler. Defaults to 1 second.
        /// </summary>
        public TimeSpan RecurringTickInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets whether this instance runs the scheduled and recurring schedulers. Defaults to true.
        /// With the EF store, a scheduler lock elects a single leader per sweep, so the
        /// setting can stay enabled on every instance.
        /// </summary>
        public bool EnableSchedulers { get; set; } = true;

        /// <summary>
        /// Adds an assembly to be scanned for job types.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public void AddAssembly(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            Assemblies.Add(assembly);
        }

        /// <summary>
        /// Adds the assembly containing the marker type to be scanned for job types.
        /// </summary>
        /// <typeparam name="TMarker">A type contained in the assembly to scan.</typeparam>
        public void AddAssembly<TMarker>()
        {
            Assemblies.Add(typeof(TMarker).Assembly);
        }

        /// <summary>
        /// Registers a recurring job. The cron expression uses five fields, or six to include seconds.
        /// Occurrences are evaluated in UTC.
        /// </summary>
        /// <typeparam name="TJob">The job type.</typeparam>
        /// <typeparam name="TArgs">The type of the job arguments.</typeparam>
        /// <param name="cron">The cron expression, for example "*/5 * * * *".</param>
        /// <param name="args">The arguments passed to every occurrence.</param>
        public void AddRecurring<TJob, TArgs>(string cron, TArgs args)
            where TJob : IJob<TArgs>
        {
            ArgumentNullException.ThrowIfNull(cron);

            Recurring.Add(new RecurringJobDefinition(cron, typeof(TJob), JsonSerializer.Serialize(args)));
        }
    }
}
