using System.Diagnostics.Metrics;
using Truss.Jobs.Storage;

namespace Truss.Jobs.Runtime
{
    /// <summary>
    /// Emits the job runtime metrics through the "Truss.Jobs" meter: a counter
    /// of executions tagged with their outcome, a duration histogram, and
    /// gauges for the queue sampled by the scheduler poller. Subscribe the
    /// meter in your OpenTelemetry configuration to export them.
    /// </summary>
    public sealed class JobMetrics : IDisposable
    {
        /// <summary>The name of the meter the metrics are emitted through.</summary>
        public const string MeterName = "Truss.Jobs";

        private readonly Meter _meter;

        /// <summary>The meter instance, so tests can tell this host's instruments apart.</summary>
        internal Meter Meter => _meter;

        private readonly Counter<long> _executed;
        private readonly Histogram<double> _duration;
        private long _queued = -1;
        private long _running = -1;
        private long _failed = -1;

        /// <summary>
        /// Initializes the meter and its instruments.
        /// </summary>
        /// <param name="meterFactory">The host's meter factory, when metrics are configured; otherwise a standalone meter is created.</param>
        public JobMetrics(IMeterFactory? meterFactory = null)
        {
            _meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

            _executed = _meter.CreateCounter<long>(
                "truss.jobs.executed", unit: "{job}", description: "Job executions by outcome: succeeded, failed, retried or cancelled.");

            _duration = _meter.CreateHistogram<double>(
                "truss.jobs.duration", unit: "s", description: "Seconds a job execution took, whatever its outcome.");

            _meter.CreateObservableGauge(
                "truss.jobs.queued", () => Observe(ref _queued), unit: "{job}", description: "Jobs waiting to run, sampled by the scheduler poller.");

            _meter.CreateObservableGauge(
                "truss.jobs.running", () => Observe(ref _running), unit: "{job}", description: "Jobs currently running, sampled by the scheduler poller.");

            _meter.CreateObservableGauge(
                "truss.jobs.failed", () => Observe(ref _failed), unit: "{job}", description: "Jobs failed permanently, sampled by the scheduler poller.");
        }

        /// <summary>
        /// Records a finished execution attempt.
        /// </summary>
        /// <param name="outcome">What happened: succeeded, failed, retried or cancelled.</param>
        /// <param name="jobName">The registered name of the job.</param>
        /// <param name="duration">How long the attempt ran.</param>
        public void Executed(string outcome, string jobName, TimeSpan duration)
        {
            _executed.Add(1,
                new KeyValuePair<string, object?>("outcome", outcome),
                new KeyValuePair<string, object?>("job", jobName));

            _duration.Record(duration.TotalSeconds,
                new KeyValuePair<string, object?>("job", jobName));
        }

        /// <summary>
        /// Publishes a fresh statistics snapshot to the queue gauges.
        /// </summary>
        /// <param name="statistics">The job counters.</param>
        public void DepthSampled(JobStatistics statistics)
        {
            ArgumentNullException.ThrowIfNull(statistics);

            Volatile.Write(ref _queued, statistics.QueuedCount);
            Volatile.Write(ref _running, statistics.RunningCount);
            Volatile.Write(ref _failed, statistics.FailedCount);
        }

        private static IEnumerable<Measurement<long>> Observe(ref long value)
        {
            var current = Volatile.Read(ref value);
            return current < 0 ? [] : [new Measurement<long>(current)];
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _meter.Dispose();
        }
    }
}
