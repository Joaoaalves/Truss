using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Truss.Application;
using Truss.Jobs;

namespace Truss.Jobs.Tests.Fakes
{
    public sealed record ReportArgs(string Target);

    [JobName("test.report")]
    public class ReportJob : IJob<ReportArgs>
    {
        public async Task Execute(ReportArgs args, JobContext context, CancellationToken cancellationToken)
        {
            await context.ReportProgress(50, $"Halfway through {args.Target}", cancellationToken);
            await context.ReportProgress(100, "Done", cancellationToken);
        }
    }

    public sealed record FailArgs(string Reason);

    [JobName("test.failing")]
    public class FailingJob : IJob<FailArgs>
    {
        public Task Execute(FailArgs args, JobContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(args.Reason);
        }
    }

    public sealed record WaitArgs(string Label);

    [JobName("test.waiting")]
    public class WaitingJob : IJob<WaitArgs>
    {
        public async Task Execute(WaitArgs args, JobContext context, CancellationToken cancellationToken)
        {
            await context.ReportProgress(10, "Waiting", cancellationToken);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(25, cancellationToken);
            }
        }
    }

    public sealed record TickArgs(string Source);

    public class TickCounter
    {
        private int _count;

        public int Count => _count;

        public void Increment() => Interlocked.Increment(ref _count);
    }

    [JobName("test.tick")]
    public class TickJob(TickCounter counter) : IJob<TickArgs>
    {
        public Task Execute(TickArgs args, JobContext context, CancellationToken cancellationToken)
        {
            counter.Increment();
            return Task.CompletedTask;
        }
    }

    public class EnqueuedJobs
    {
        public ConcurrentQueue<Guid> Ids { get; } = new();
    }

    public sealed record StartReportCommand(string Target) : ICommand<Guid>;

    public class StartReportCommandHandler(IJobScheduler scheduler) : ICommandHandler<StartReportCommand, Guid>
    {
        public Task<Guid> Handle(StartReportCommand request, CancellationToken cancellationToken)
        {
            return scheduler.Enqueue<ReportJob, ReportArgs>(new ReportArgs(request.Target), cancellationToken);
        }
    }

    public sealed record FailingEnqueueCommand : ICommand;

    public class FailingEnqueueCommandHandler(IJobScheduler scheduler) : ICommandHandler<FailingEnqueueCommand>
    {
        public async Task<Unit> Handle(FailingEnqueueCommand request, CancellationToken cancellationToken)
        {
            await scheduler.Enqueue<ReportJob, ReportArgs>(new ReportArgs("never"), cancellationToken);
            throw new InvalidOperationException("Handler failed.");
        }
    }

    public class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyTrussOutbox();
            modelBuilder.ApplyTrussJobs();
        }
    }
}
