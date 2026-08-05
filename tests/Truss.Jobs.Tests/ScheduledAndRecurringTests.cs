using Microsoft.Extensions.DependencyInjection;
using Truss.Jobs.Tests.Fakes;
using Xunit;

namespace Truss.Jobs.Tests
{
    public class ScheduledAndRecurringTests
    {
        [Fact]
        public async Task ScheduledJob_RunsWhenDue()
        {
            await using var host = new JobsTestHost();

            Guid jobId;

            using (var scope = host.Provider.CreateScope())
            {
                var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
                jobId = await scheduler.Schedule<ReportJob, ReportArgs>(
                    new ReportArgs("later"), DateTimeOffset.UtcNow.AddMilliseconds(300));
                await scope.ServiceProvider.GetRequiredService<Truss.Application.IUnitOfWork>().CommitAsync();
            }

            var beforeDue = await host.Snapshot(jobId);
            Assert.Equal(JobStatus.Scheduled, beforeDue!.Status);

            var snapshot = await host.WaitForStatus(jobId, JobStatus.Succeeded);
            Assert.NotNull(snapshot.ScheduledFor);
        }

        [Fact]
        public async Task RecurringJob_RunsRepeatedly()
        {
            await using var host = new JobsTestHost(options =>
                options.AddRecurring<TickJob, TickArgs>("* * * * * *", new TickArgs("cron")));

            var counter = host.Provider.GetRequiredService<TickCounter>();
            var deadline = DateTime.UtcNow.AddSeconds(15);

            while (DateTime.UtcNow < deadline && counter.Count < 2)
                await Task.Delay(100);

            Assert.True(counter.Count >= 2, $"Recurring job ran {counter.Count} times.");
        }
    }
}
