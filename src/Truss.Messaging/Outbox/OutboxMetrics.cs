using System.Diagnostics.Metrics;

namespace Truss.Messaging.Outbox
{
    /// <summary>
    /// Emits the outbox operability metrics through the "Truss.Messaging" meter:
    /// counters for published messages and failed attempts, a histogram of the
    /// lag between an event occurring and its publish, and gauges for queue
    /// depth and dead letters sampled by the processor. Subscribe the meter in
    /// your OpenTelemetry configuration to export them.
    /// </summary>
    public sealed class OutboxMetrics : IDisposable
    {
        /// <summary>The name of the meter the metrics are emitted through.</summary>
        public const string MeterName = "Truss.Messaging";

        private readonly Meter _meter;

        /// <summary>The meter instance, so tests can tell this host's instruments apart.</summary>
        internal Meter Meter => _meter;

        private readonly Counter<long> _published;
        private readonly Counter<long> _failures;
        private readonly Histogram<double> _publishLag;
        private long _pending = -1;
        private long _deadLettered = -1;

        /// <summary>
        /// Initializes the meter and its instruments.
        /// </summary>
        /// <param name="meterFactory">The host's meter factory, when metrics are configured; otherwise a standalone meter is created.</param>
        public OutboxMetrics(IMeterFactory? meterFactory = null)
        {
            _meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

            _published = _meter.CreateCounter<long>(
                "truss.outbox.published", unit: "{message}", description: "Messages published to the transport.");

            _failures = _meter.CreateCounter<long>(
                "truss.outbox.publish_failures", unit: "{attempt}", description: "Publish attempts that failed; dead_lettered marks the final one.");

            _publishLag = _meter.CreateHistogram<double>(
                "truss.outbox.publish_lag", unit: "s", description: "Seconds between an event occurring and its publish.");

            _meter.CreateObservableGauge(
                "truss.outbox.pending", ObservePending, unit: "{message}", description: "Messages waiting for publication, sampled by the processor.");

            _meter.CreateObservableGauge(
                "truss.outbox.dead_lettered", ObserveDeadLettered, unit: "{message}", description: "Messages dead-lettered after exhausting their attempts, sampled by the processor.");
        }

        /// <summary>
        /// Records a successful publish and how long the message waited for it.
        /// </summary>
        /// <param name="lag">The time between the event occurring and the publish.</param>
        public void Published(TimeSpan lag)
        {
            _published.Add(1);
            _publishLag.Record(lag.TotalSeconds);
        }

        /// <summary>
        /// Records a failed publish attempt.
        /// </summary>
        /// <param name="deadLettered">Whether the failure was the message's last attempt.</param>
        public void PublishFailed(bool deadLettered)
        {
            _failures.Add(1, new KeyValuePair<string, object?>("dead_lettered", deadLettered));
        }

        /// <summary>
        /// Publishes a fresh statistics snapshot to the depth gauges.
        /// </summary>
        /// <param name="statistics">The outbox counters.</param>
        public void DepthSampled(OutboxStatistics statistics)
        {
            ArgumentNullException.ThrowIfNull(statistics);

            Volatile.Write(ref _pending, statistics.PendingCount);
            Volatile.Write(ref _deadLettered, statistics.FailedCount);
        }

        private IEnumerable<Measurement<long>> ObservePending()
        {
            var pending = Volatile.Read(ref _pending);
            return pending < 0 ? [] : [new Measurement<long>(pending)];
        }

        private IEnumerable<Measurement<long>> ObserveDeadLettered()
        {
            var deadLettered = Volatile.Read(ref _deadLettered);
            return deadLettered < 0 ? [] : [new Measurement<long>(deadLettered)];
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _meter.Dispose();
        }
    }
}
