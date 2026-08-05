using Microsoft.Extensions.DependencyInjection;
using Truss.Jobs.Tests.Fakes;
using Xunit;

namespace Truss.Jobs.Tests
{
    public class JobExecutionTests
    {
        [Fact]
        public async Task EnqueuedJob_Executes_AndRecordsProgress()
        {
            await using var host = new JobsTestHost();

            var jobId = await host.Send(new StartReportCommand("catalog"));

            var snapshot = await host.WaitForStatus(jobId, JobStatus.Succeeded);
            Assert.Equal(100, snapshot.ProgressPercent);
            Assert.Equal("Done", snapshot.ProgressMessage);
            Assert.Equal(1, snapshot.Attempts);
            Assert.NotNull(snapshot.StartedOn);
            Assert.NotNull(snapshot.CompletedOn);
        }

        [Fact]
        public async Task FailingJob_Retries_ThenFailsPermanently()
        {
            await using var host = new JobsTestHost();

            Guid jobId;

            using (var scope = host.Provider.CreateScope())
            {
                var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
                jobId = await scheduler.Enqueue<FailingJob, FailArgs>(new FailArgs("boom"));
                await scope.ServiceProvider.GetRequiredService<Truss.Application.IUnitOfWork>().CommitAsync();
            }

            var snapshot = await host.WaitForStatus(jobId, JobStatus.Failed);
            Assert.Equal(2, snapshot.Attempts);
            Assert.Equal("boom", snapshot.Error);
        }
    }
}
