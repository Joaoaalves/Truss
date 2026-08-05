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

At the edge, `Truss.Observability.AspNetCore` bridges HTTP:

```csharp
app.UseTrussCorrelation();
```

The middleware reads `X-Correlation-Id` from the request (or creates one), makes it ambient for everything the request touches and echoes it back in the response header, so clients can quote the id when reporting a problem. Outside HTTP, hosts can seed the ambient id through `ExecutionContextHolder.Current`; when nothing sets it, the logging behavior creates one per dispatch so log entries still correlate.

---

## Traces and Metrics

Truss emits spans through three activity sources and metrics through one meter, all BCL primitives with negligible cost when nothing listens:

| Source | Spans |
|---|---|
| `Truss.Application` | One per request, tagged with name and kind, error status on failure |
| `Truss.Messaging` | One per outbox publish and one per consumed message |
| `Truss.Jobs` | One per job execution, tagged with job name, id and attempt |

| Metric | Type | Tags |
|---|---|---|
| `truss.requests` | Counter | request, kind, outcome (success, rejected, failure) |
| `truss.request.duration` | Histogram (ms) | request, kind, outcome |

Wire them to the OpenTelemetry SDK in the composition root:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("Truss.Application", "Truss.Messaging", "Truss.Jobs")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("Truss")
        .AddOtlpExporter());
```

---

## Dashboards

Truss does not bundle a log viewer; it emits standard signals and any free stack consumes them:

- **Development**: a Seq container gives instant structured log search. Point the OpenTelemetry OTLP exporter (or a Seq sink) at `http://localhost:5341`.

```yaml
services:
  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: "Y"
    ports:
      - "5341:80"
```

- **Production, fully free**: Grafana with Loki (logs), Tempo (traces) and Prometheus (metrics), all fed by the OTLP exporter through an OpenTelemetry Collector.

The Truss CLI will generate these compose files as part of project scaffolding. See the [Roadmap](roadmap.md).
