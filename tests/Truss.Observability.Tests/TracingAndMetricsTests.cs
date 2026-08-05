using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Observability.Tests.Fakes;
using Xunit;

namespace Truss.Observability.Tests
{
    public class TracingAndMetricsTests
    {
        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTruss(options => options.AddAssembly<PingCommand>());
            services.AddTrussObservability();
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Dispatch_EmitsActivity_WithRequestTags()
        {
            var stopped = new System.Collections.Concurrent.ConcurrentQueue<Activity>();

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Truss.Application",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = stopped.Enqueue
            };

            ActivitySource.AddActivityListener(listener);

            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new TracedCommand());

            var activity = stopped.FirstOrDefault(a => a.DisplayName == nameof(TracedCommand));
            Assert.NotNull(activity);
            Assert.Equal("command", activity.GetTagItem("truss.request.kind"));
            Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
        }

        [Fact]
        public async Task FailedDispatch_EmitsActivity_WithErrorStatus()
        {
            var stopped = new System.Collections.Concurrent.ConcurrentQueue<Activity>();

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Truss.Application",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = stopped.Enqueue
            };

            ActivitySource.AddActivityListener(listener);

            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new TracedThrowingCommand())
            );

            var activity = stopped.FirstOrDefault(a => a.DisplayName == nameof(TracedThrowingCommand));
            Assert.NotNull(activity);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
        }

        [Fact]
        public async Task Dispatch_RecordsRequestMetrics()
        {
            var measurements = new List<(long Value, string? Outcome)>();

            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == "Truss" && instrument.Name == "truss.requests")
                    meterListener.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
            {
                string? outcome = null;

                foreach (var tag in tags)
                {
                    if (tag.Key == "truss.outcome")
                        outcome = tag.Value?.ToString();
                }

                lock (measurements)
                {
                    measurements.Add((value, outcome));
                }
            });
            listener.Start();

            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new PingCommand("abc"));

            listener.RecordObservableInstruments();

            lock (measurements)
            {
                Assert.Contains(measurements, m => m.Value == 1 && m.Outcome == "success");
            }
        }
    }
}
