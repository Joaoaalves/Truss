# Integration Testing

`Truss.Testing` boots your application the way production runs it, in a few lines: the full pipeline with validation and the unit of work, a throwaway sqlite database built from your own context model, the in-memory transport with the outbox, and the job runtime with schedulers tuned for fast tests. A test exercises the same code path an endpoint would, without HTTP in the way.

```csharp
await using var host = await TrussTestHost.Start<AppDbContext>(options =>
{
    options.AddAssembly<PlaceOrder>();
    options.UseJobs();
    options.ConfigureServices(services => services.AddScoped<IOrderRepository, EfOrderRepository>());
});

var orderId = await host.Send(new PlaceOrder(items));

var stored = await host.ExecuteScoped(provider =>
    provider.GetRequiredService<AppDbContext>().Orders.SingleAsync(order => order.Id == orderId));
```

`Send` dispatches through the real pipeline in its own scope: validators run and throw `RequestValidationException` with every failure, business rules surface as violations, and the unit of work commits when the handler succeeds. What your test observes is what production does.

---

## Starting a Host

`Start<TDbContext>` creates a temporary sqlite database from your context's own model and deletes it on dispose. Because `OnModelCreating` is yours, the Truss tables you applied (`ApplyTrussOutbox`, `ApplyTrussJobs`) exist exactly as in the real application. Each host gets its own database, so tests parallelize safely.

`Start` without a context boots a database-free host: messaging publishes directly and jobs use the in-memory store. Good for testing handlers that touch no persistence.

| Option | Effect |
|---|---|
| `AddAssembly<TMarker>()` | Scans the assembly for handlers, events and jobs |
| `UseMessaging()` | In-memory transport; with a database, the outbox too, tuned fast |
| `UseJobs()` | Job runtime with fast schedulers; implies messaging |
| `ConfigureServices(...)` | Your registrations, applied last so replacements win |

---

## Messaging and Jobs Helpers

```csharp
var orderId = await host.Send(new PlaceOrder(items));
await host.DrainOutbox();

var received = host.Services.GetRequiredService<ReceivedEvents>();
Assert.Contains(received.Events, e => e.OrderId == orderId);
```

`DrainOutbox` publishes every pending outbox message and waits until the in-memory transport has handled everything, so event delivery becomes deterministic instead of racing the background services. For jobs:

```csharp
var jobId = await host.Send(new StartExport("catalog"));

var snapshot = await host.WaitForJob(jobId, JobStatus.Succeeded);
Assert.Equal(100, snapshot.ProgressPercent);
```

`WaitForJob` polls the job monitor until the status arrives and fails with the last observed state on timeout. `GetJob` returns a single snapshot when you only need to look.

---

## What It Is Not

The host runs your application layer against real persistence, not your HTTP surface: route bindings, auth policies and ProblemDetails responses belong to endpoint tests with `Microsoft.AspNetCore.TestHost`, which compose naturally with the same scaffolded application. And sqlite is not your production database; keep a smaller set of tests against the real provider for provider-specific behavior.
