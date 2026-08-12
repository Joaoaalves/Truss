using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Truss.Jobs.Tests.Fakes;
using Xunit;

namespace Truss.Jobs.Tests
{
    /// <summary>
    /// The job runtime reports executions through the "Truss.Jobs" meter,
    /// tagged with their outcome, so a dashboard can tell a healthy queue from
    /// one that only retries.
    /// </summary>
    public class JobMetricsTests
    {
        [Fact]
        public async Task Executions_ShowUpInTheMeter_TaggedWithTheirOutcome()
        {
            await using var host = new JobsTestHost(options => options.RetryBaseDelay = TimeSpan.FromMilliseconds(50));

            var meter = host.Provider.GetRequiredService<JobMetrics>().Meter;
            var outcomes = new ConcurrentDictionary<string, long>();
            var durations = new ConcurrentBag<double>();

            using var listener = new MeterListener();

            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter == meter)
                    meterListener.EnableMeasurementEvents(instrument);
            };

            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                if (instrument.Name != "truss.jobs.executed")
                    return;

                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome" && tag.Value is string outcome)
                        outcomes.AddOrUpdate(outcome, measurement, (_, total) => total + measurement);
                }
            });

            listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
                durations.Add(measurement));

            listener.Start();

            using (var scope = host.Provider.CreateScope())
            {
                var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
                var succeedingId = await scheduler.Enqueue<ReportJob, ReportArgs>(new ReportArgs("metrics"));
                var failingId = await scheduler.Enqueue<FailingJob, FailArgs>(new FailArgs("boom"));
                await scope.ServiceProvider.GetRequiredService<Truss.Application.IUnitOfWork>().CommitAsync();

                await host.WaitForStatus(succeedingId, JobStatus.Succeeded);
                await host.WaitForStatus(failingId, JobStatus.Failed);
            }

            Assert.Equal(1, outcomes["succeeded"]);
            Assert.Equal(1, outcomes["retried"]);
            Assert.Equal(1, outcomes["failed"]);
            Assert.NotEmpty(durations);
        }
    }
}
