using Microsoft.Extensions.DependencyInjection;
using Truss.Jobs.EntityFrameworkCore;
using Truss.Jobs.Tests.Fakes;
using Xunit;

namespace Truss.Jobs.Tests
{
    public class JobLifecycleTests
    {
        private static async Task<Guid> Enqueue<TJob, TArgs>(JobsTestHost host, TArgs args)
            where TJob : IJob<TArgs>
        {
            using var scope = host.Provider.CreateScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
            var jobId = await scheduler.Enqueue<TJob, TArgs>(args);
            await scope.ServiceProvider.GetRequiredService<Truss.Application.IUnitOfWork>().CommitAsync();
            return jobId;
        }

        private static async Task<bool> Cancel(JobsTestHost host, Guid jobId)
        {
            using var scope = host.Provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IJobScheduler>().Cancel(jobId);
        }

        [Fact]
        public async Task FailingJob_BacksOff_BeforeTheNextAttempt()
        {
            await using var host = new JobsTestHost(options => options.RetryBaseDelay = TimeSpan.FromMilliseconds(500));

            var jobId = await Enqueue<FailingJob, FailArgs>(host, new FailArgs("boom"));

            var scheduled = await host.WaitForStatus(jobId, JobStatus.Scheduled);
            Assert.Equal(1, scheduled.Attempts);
            Assert.NotNull(scheduled.ScheduledFor);

            var failed = await host.WaitForStatus(jobId, JobStatus.Failed);
            Assert.Equal(2, failed.Attempts);
            Assert.Equal("boom", failed.Error);
        }

        [Fact]
        public async Task ScheduledJob_CancelledBeforeItsMoment_NeverRuns()
        {
            await using var host = new JobsTestHost();

            Guid jobId;

            using (var scope = host.Provider.CreateScope())
            {
                var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
                jobId = await scheduler.Schedule<ReportJob, ReportArgs>(
                    new ReportArgs("later"), DateTimeOffset.UtcNow.AddSeconds(2));
                await scope.ServiceProvider.GetRequiredService<Truss.Application.IUnitOfWork>().CommitAsync();
            }

            Assert.True(await Cancel(host, jobId));

            var snapshot = await host.WaitForStatus(jobId, JobStatus.Cancelled);
            Assert.Equal(0, snapshot.Attempts);
            Assert.NotNull(snapshot.CompletedOn);

            await Task.Delay(2500);
            Assert.Equal(JobStatus.Cancelled, (await host.Snapshot(jobId))!.Status);
        }

        [Fact]
        public async Task RunningJob_ObservesCancellation_AndStops()
        {
            await using var host = new JobsTestHost();

            var jobId = await Enqueue<WaitingJob, WaitArgs>(host, new WaitArgs("long"));

            await host.WaitForStatus(jobId, JobStatus.Running);
            Assert.True(await Cancel(host, jobId));

            var snapshot = await host.WaitForStatus(jobId, JobStatus.Cancelled);
            Assert.Equal(1, snapshot.Attempts);
        }

        [Fact]
        public async Task Cancel_UnknownJob_ReturnsFalse()
        {
            await using var host = new JobsTestHost();

            Assert.False(await Cancel(host, Guid.NewGuid()));
        }

        [Fact]
        public async Task FinishedJobs_AreSweptAfterRetention_WhileFailedOnesStay()
        {
            await using var host = new JobsTestHost(options =>
            {
                options.RetentionPeriod = TimeSpan.FromMilliseconds(1);
                options.CleanupInterval = TimeSpan.Zero;
            });

            var succeededId = await Enqueue<ReportJob, ReportArgs>(host, new ReportArgs("catalog"));
            var failedId = await Enqueue<FailingJob, FailArgs>(host, new FailArgs("boom"));

            await host.WaitForStatus(failedId, JobStatus.Failed);

            // Only succeeded jobs are swept, so the record disappearing is itself
            // the proof that the job finished and retention removed it.
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (await host.Snapshot(succeededId) is not null && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            Assert.Null(await host.Snapshot(succeededId));
            Assert.Equal(JobStatus.Failed, (await host.Snapshot(failedId))!.Status);
        }

        [Fact]
        public async Task Statistics_AndHealthCheck_ReportFailedJobs()
        {
            await using var host = new JobsTestHost();

            var jobId = await Enqueue<FailingJob, FailArgs>(host, new FailArgs("boom"));
            await host.WaitForStatus(jobId, JobStatus.Failed);

            using var scope = host.Provider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

            var statistics = await store.GetStatistics();
            Assert.Equal(1, statistics.FailedCount);

            var check = new JobsHealthCheck(store);
            var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

            Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, result.Status);
        }

        [Fact]
        public async Task SchedulerLock_GrantsOneOwner_AndHandsOverAfterExpiry()
        {
            await using var host = new JobsTestHost(startHostedServices: false);

            using var scope = host.Provider.CreateScope();
            Assert.IsType<EfSchedulerLock<Fakes.JobsDbContext>>(scope.ServiceProvider.GetRequiredService<ISchedulerLock>());

            // The lease is a clock question, so the test owns the clock: sleeping
            // for a real lease makes the result depend on how busy the machine is.
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            var schedulerLock = new EfSchedulerLock<Fakes.JobsDbContext>(
                scope.ServiceProvider.GetRequiredService<Fakes.JobsDbContext>(), clock);

            var lease = TimeSpan.FromMinutes(1);

            Assert.True(await schedulerLock.TryAcquire("test.lock", "instance-a", lease));
            Assert.False(await schedulerLock.TryAcquire("test.lock", "instance-b", lease));
            Assert.True(await schedulerLock.TryAcquire("test.lock", "instance-a", lease));

            clock.Advance(lease + TimeSpan.FromSeconds(1));

            Assert.True(await schedulerLock.TryAcquire("test.lock", "instance-b", lease));
            Assert.False(await schedulerLock.TryAcquire("test.lock", "instance-a", lease));

            // A graceful shutdown releases the lease, so the other instance
            // takes over immediately instead of waiting out the expiry.
            await schedulerLock.Release("test.lock", "instance-b");
            Assert.True(await schedulerLock.TryAcquire("test.lock", "instance-a", lease));

            // Releasing a lease the caller does not hold changes nothing.
            await schedulerLock.Release("test.lock", "instance-b");
            Assert.False(await schedulerLock.TryAcquire("test.lock", "instance-b", lease));
        }

        private sealed class MutableClock(DateTimeOffset now) : TimeProvider
        {
            private DateTimeOffset _now = now;

            public override DateTimeOffset GetUtcNow() => _now;

            public void Advance(TimeSpan amount) => _now += amount;
        }
    }
}
