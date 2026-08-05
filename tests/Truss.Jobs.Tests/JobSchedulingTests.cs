using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Jobs.Tests.Fakes;
using Xunit;

namespace Truss.Jobs.Tests
{
    public class JobSchedulingTests
    {
        [Fact]
        public async Task EnqueueInsideCommand_CreatesJobRecordAtomically()
        {
            await using var host = new JobsTestHost(startHostedServices: false);

            var jobId = await host.Send(new StartReportCommand("catalog"));

            var snapshot = await host.Snapshot(jobId);
            Assert.NotNull(snapshot);
            Assert.Equal(JobStatus.Queued, snapshot.Status);
            Assert.Equal("test.report", snapshot.Name);
        }

        [Fact]
        public async Task FailingCommand_CreatesNoJobRecord()
        {
            await using var host = new JobsTestHost(startHostedServices: false);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.Send(new FailingEnqueueCommand())
            );

            using var scope = host.Provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            Assert.Equal(0, await context.Set<JobRecord>().CountAsync());
            Assert.Equal(0, await context.Set<Truss.Messaging.OutboxMessage>().CountAsync());
        }
    }
}
