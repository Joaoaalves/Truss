# Background Jobs

`Truss.Jobs` runs work outside the request: long imports, report generation, anything that should not block a response. It is built on top of [messaging](messaging.md), so job delivery inherits the outbox transactionality and the durability of whatever transport you chose. There is no separate broker to configure.

---

## Defining a Job

A job is a class with typed, JSON-serializable arguments:

```csharp
public sealed record GenerateReportArgs(Guid CustomerId, int Year);

[JobName("reports.generate")]
public class GenerateReportJob(AppDbContext context) : IJob<GenerateReportArgs>
{
    public async Task Execute(GenerateReportArgs args, JobContext jobContext, CancellationToken cancellationToken)
    {
        var orders = await context.Orders.Where(o => o.CustomerId == args.CustomerId).ToListAsync(cancellationToken);

        for (var i = 0; i < orders.Count; i++)
        {
            await ProcessOrder(orders[i], cancellationToken);
            await jobContext.ReportProgress(i * 100 / orders.Count, $"Processed {i + 1} of {orders.Count}");
        }
    }
}
```

Jobs are resolved from a dependency injection scope per execution, so constructor injection works as anywhere else. The `JobName` attribute gives the job a stable name stored with each record; without it, the full CLR type name is used.

Delivery is at-least-once: a job may run more than once for the same arguments, so make it idempotent.

---

## Scheduling

Inject `IJobScheduler`:

```csharp
public class RequestReportHandler(IJobScheduler scheduler) : ICommandHandler<RequestReport, Guid>
{
    public Task<Guid> Handle(RequestReport command, CancellationToken cancellationToken)
    {
        return scheduler.Enqueue<GenerateReportJob, GenerateReportArgs>(
            new GenerateReportArgs(command.CustomerId, command.Year), cancellationToken);
    }
}
```

Called inside a command handler, scheduling is **transactional**: the job record and its trigger message are stored by the same atomic save as the rest of the command. If the command fails, the job never existed. The returned id is what clients use to track progress.

`Schedule` runs a job at a specific moment:

```csharp
await scheduler.Schedule<GenerateReportJob, GenerateReportArgs>(args, DateTimeOffset.UtcNow.AddHours(2));
```

Recurring jobs are declared at registration with a cron expression (five fields, or six to include seconds; evaluated in UTC):

```csharp
services.AddTrussJobs(options =>
{
    options.AddAssembly<GenerateReportJob>();
    options.AddRecurring<CleanupJob, CleanupArgs>("0 3 * * *", new CleanupArgs(olderThanDays: 30));
});
```

---

## Execution, Retry and Timeout

A queued job flows through the messaging pipeline: outbox, transport, consumer. The executor loads the record, marks it running and invokes the job. On failure the job waits an exponential backoff and runs again until the attempt limit, then fails permanently with the error preserved. Between attempts the record shows as scheduled, with the next attempt moment in `ScheduledFor`. A timeout can bound each attempt.

| Setting | Default | Meaning |
|---|---|---|
| `MaxAttempts` | 3 | Attempts before the job fails permanently |
| `JobTimeout` | none | Time limit per attempt |
| `RetryBaseDelay` | 5 s | Base of the exponential backoff; zero retries immediately |
| `RetryMaxDelay` | 5 min | Upper bound of the backoff |
| `ScheduledPollingInterval` | 5 s | How often due scheduled jobs are queued |
| `EnableSchedulers` | true | Runs the scheduled and recurring services on this instance |
| `SchedulerLockLeaseDuration` | 30 s | Lease of the scheduler lock in multi-instance deployments |
| `CancellationPollingInterval` | 2 s | How often a running job checks for a cancellation request |
| `RetentionPeriod` | 7 days | How long succeeded and cancelled jobs are kept; null keeps forever |
| `CleanupInterval` | 1 h | How often the retention sweep runs |

All settings bind from configuration, for example `Truss__Jobs__MaxAttempts=5`.

### Multiple instances

With the EF store, the schedulers elect a leader through a lease-based lock stored next to the job records, so `EnableSchedulers` can stay enabled everywhere: one instance sweeps scheduled and recurring jobs at a time, and when it stops, another takes over once the lease expires. Execution itself is already distributed by the transport; the lock only covers who enqueues.

---

## Cancelling

Request cancellation through the scheduler or the endpoint:

```csharp
await scheduler.Cancel(jobId);
```

A queued or scheduled job is cancelled immediately and never runs. A running job observes the request through the `CancellationToken` passed to `Execute`: the token fires within the cancellation polling interval, and a job that honors it ends as cancelled. Jobs already in a terminal state are left untouched.

---

## Retention

Succeeded and cancelled jobs are deleted after `RetentionPeriod`, 7 days by default. Failed jobs are never deleted; they stay for inspection. The sweep runs with the scheduler lock, so a single instance cleans.

---

## Metrics

The runtime reports itself through the `Truss.Jobs` meter:

| Instrument | Kind | Meaning |
|---|---|---|
| `truss.jobs.executed` | counter | Executions, tagged with `job` and `outcome`: succeeded, failed, retried or cancelled |
| `truss.jobs.duration` | histogram | Seconds an execution took, tagged with `job` |
| `truss.jobs.queued` | gauge | Jobs waiting to run, sampled by the scheduler poller |
| `truss.jobs.running` | gauge | Jobs currently running, sampled by the scheduler poller |
| `truss.jobs.failed` | gauge | Jobs failed permanently, sampled by the scheduler poller |

With the [observability module](observability.md), `AddTrussOpenTelemetry` exports them automatically. A queue that only ever increments `retried` is the first sign of a poisoned job, well before the health check degrades.

---

## Tracking Progress

Inside the job, report progress through the context; each report is persisted immediately:

```csharp
await jobContext.ReportProgress(40, "Importing prices");
```

With `Truss.Jobs.AspNetCore`, expose the progress endpoints:

```csharp
app.MapTrussJobs();
```

| Endpoint | Behavior |
|---|---|
| `GET /truss/jobs/{id}` | Returns the current snapshot: status, attempts, progress, error, timestamps |
| `GET /truss/jobs/{id}/stream` | Server-sent events: pushes a snapshot on every change interval until the job completes |
| `POST /truss/jobs/{id}/cancel` | Requests cancellation and returns the resulting snapshot |

The snapshot serializes with camelCase names and string statuses:

```json
{
  "id": "7d9f4c1e-...",
  "name": "reports.generate",
  "status": "running",
  "attempts": 1,
  "progressPercent": 40,
  "progressMessage": "Importing prices"
}
```

Streaming uses server-sent events because every HTTP client and browser (`EventSource`) understands it, with no extra dependency. Polling the snapshot endpoint gives the same data for clients that prefer it. `MapTrussJobs` returns the route group, so `RequireAuthorization` and friends chain as usual.

---

## Registration

```csharp
services.AddTrussMessaging(options => options.AddAssembly<OrderPlaced>());
services.AddTrussInMemoryTransport();
services.AddTrussOutbox<AppDbContext>();

services.AddTrussJobs(options => options.AddAssembly<GenerateReportJob>());
services.AddTrussJobsEntityFramework<AppDbContext>();
```

And add the job table to the context model:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyTrussOutbox();
    modelBuilder.ApplyTrussJobs();
}
```

`AddTrussJobs` requires messaging to be registered first. Without `AddTrussJobsEntityFramework`, an in-memory store is used: fine for development, lost on restart.

Because jobs ride the messaging pipeline, choosing where jobs run is a transport decision: in-process with the in-memory transport, or distributed across instances with the [Postgres, RabbitMQ or Redis transports](messaging.md), where any instance can pick up the work.
