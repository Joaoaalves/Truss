# Observability

`Truss.Observability` makes the framework tell you what it is doing: every request logged with a structured scope, spans and metrics for requests, messages and jobs, and a correlation id tying it all together. Everything is opt-in and adds no exporter dependency: Truss emits through `Microsoft.Extensions.Logging` and the BCL diagnostics primitives, and you choose what listens.

---

## Registration

```csharp
services.AddTrussObservability();
```

Both behaviors are enabled by default and can be turned off individually:

```csharp
services.AddTrussObservability(options =>
{
    options.EnableLogging = true;
    options.EnableTracing = false;
});
```

Registration order does not matter: the observability behaviors place themselves at the outermost position of the pipeline, so they also observe validation rejections and unit of work failures.

---

## Structured Logging

Every dispatched request is logged with a scope carrying the request name and the correlation id:

```
info: Handled CreateUser in 12.3ms
warn: Rejected CreateUser with 2 validation failures after 1.1ms
fail: Failed CreateUser after 20.5ms
      System.InvalidOperationException: ...
```

Outcomes map to sensible levels: success is Information, validation rejection is Warning (client mistakes are not system errors), everything else is Error with the exception attached. Because domain event handlers run inside the dispatch, their log entries share the same scope and correlation id.

The framework logs through `Microsoft.Extensions.Logging` only. Use any provider: console JSON, Serilog, OpenTelemetry log exporters.

---

## Correlation

`IExecutionContext` gives any handler the ambient correlation id:

```csharp
public class CreateUserHandler(IExecutionContext execution) : ICommandHandler<CreateUser, Guid>
{
    // execution.CorrelationId ties this execution to logs, spans and the HTTP response
}
```

At the edge, `Truss.AspNetCore` bridges HTTP:

```csharp
app.UseTrussCorrelation();
```

The middleware reads `X-Correlation-Id` from the request (or creates one), makes it ambient for everything the request touches and echoes it back in the response header, so clients can quote the id when reporting a problem. Outside HTTP, hosts can seed the ambient id through `ExecutionContextHolder.Current`; when nothing sets it, the logging behavior creates one per dispatch so log entries still correlate.

---

## Traces and Metrics

Truss emits spans through three activity sources and metrics through three meters, all BCL primitives with negligible cost when nothing listens:

| Source | Spans |
|---|---|
| `Truss.Application` | One per request, tagged with name and kind, error status on failure |
| `Truss.Messaging` | One per outbox publish and one per consumed message; both join the trace of the command that published the event, across the transport |
| `Truss.Jobs` | One per job execution, tagged with job name, id and attempt |

| Meter | Metrics |
|---|---|
| `Truss` | `truss.requests` counter and `truss.request.duration` histogram, tagged with request, kind and outcome |
| `Truss.Messaging` | Outbox counters, publish lag and depth gauges; see [messaging](messaging.md) |
| `Truss.Jobs` | Execution counter by outcome, duration and queue gauges; see [jobs](jobs.md) |

`Truss.Observability.OpenTelemetry` exports everything over OTLP with one registration:

```csharp
builder.Services.AddTrussOpenTelemetry();
```

It subscribes to the three Truss sources and the three Truss meters, adds the ASP.NET Core and HttpClient instrumentation, exports the application logs, and reports the service name from the entry assembly. The destination follows the standard environment variables (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`), so any OTLP backend works without code changes; the options type overrides the endpoint, the service name, or disables individual signals. Applications that need instrumentation beyond what the bridge covers can keep using the OpenTelemetry SDK directly; the package is a convenience, not a wall.

---

## Dashboards

Truss does not bundle a log viewer; it emits standard signals and any free dashboard consumes them. The CLI wires one in a single command:

```
truss add observability --dashboard aspire
```

The command references the OpenTelemetry bridge, registers `AddTrussOpenTelemetry`, points the development environment at the dashboard through `launchSettings.json` and adds the container to `docker-compose.yml`:

| Dashboard | Image | UI | Notes |
|---|---|---|---|
| `aspire` | `mcr.microsoft.com/dotnet/aspire-dashboard` | `http://localhost:18888` | Traces, logs and metrics in one place; in-memory, made for development |
| `grafana` | `grafana/otel-lgtm` | `http://localhost:3000` | Grafana with Loki, Tempo and Prometheus in one container |
| `seq` | `datalust/seq` | `http://localhost:8081` | Structured log search with native OTLP ingestion |

Run `docker compose up`, start the application and the signals appear. Switching later is one command with a different choice; only the destination changes.

For production, the same OTLP output feeds whatever the environment offers: a provisioned Grafana stack, a managed collector, or the single-container `grafana/otel-lgtm` for small deployments. Configure the destination per environment with `OTEL_EXPORTER_OTLP_ENDPOINT`; nothing in the application changes.

---

## Health Checks

Truss plugs into the standard ASP.NET Core health checks; each module contributes a check over its own data:

```csharp
builder.Services.AddHealthChecks()
    .AddTrussDatabase<AppDbContext>()
    .AddTrussOutbox()
    .AddTrussJobs();

app.MapHealthChecks("/health");
```

| Check | Degraded when | Unhealthy when |
|---|---|---|
| `truss-database` | | The database does not answer a connection attempt |
| `truss-outbox` | Dead-letters exist, or the oldest pending message is older than `MaxPendingAge` (5 minutes by default) | The store is unreachable |
| `truss-jobs` | Permanently failed jobs wait for inspection | The store is unreachable |

Each check carries its counters in the response data (pending, failed, queued, running), so a JSON response writer hands dashboards the numbers for free. There is no separate broker ping: a broker outage shows up as outbox lag, which the outbox check watches. Scaffolded projects map `/health` and register the database check from the start; `truss dev` prints the URL.
