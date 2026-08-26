# Integration Testing

`Truss.Testing` boots your application the way production runs it, in a few lines: the full pipeline with validation and the unit of work, a throwaway sqlite database built from your own context model, the in-memory transport with the outbox, and the job runtime with schedulers tuned for fast tests. A test exercises the same code path an endpoint would, without HTTP in the way.

Scaffolded projects come with two test projects wired to this from day one (`truss new` creates them unless `--no-tests`; `truss add tests` brings them to an existing project): `tests/MyShop.Domain.Tests` for pure unit tests of aggregates and rules, and `tests/MyShop.IntegrationTests` on the `TrussTestHost`. The split is deliberate: test the domain directly, test behavior through the pipeline, and do not mock what the pipeline already gives you; handlers rarely deserve isolated unit tests with mocked repositories. Generators keep the suite growing with the code: a generated aggregate arrives with its domain test, and `--crud` arrives with an integration test driving the whole slice.

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

The two known sqlite mines are worth naming, because both pass against Postgres and fail only under the test host. `DateTimeOffset` is not comparable in queries: a `Where(x => x.SyncedOn < threshold)` that works in production will not translate; map the column as a UTC instant with a value converter if you query over it. And `decimal` ordering and aggregation go through client evaluation or fail to translate, for the same storage reason. When one of these appears, the exception names the expression that could not be translated.

Tenancy is registered by the host automatically: the context keeps its own `OnModelCreating`, so a model that applies tenant isolation arrives with its query filter, and the host registers the services feeding that filter. Set the ambient tenant inside each test method (`TenantContextHolder.Current = ...`); setting it in the class constructor does not flow into the test's async context.
