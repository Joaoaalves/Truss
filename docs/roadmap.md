# Roadmap

Truss is built module by module, each one opt-in. The framework never installs anything you did not ask for.

---

## Delivered

- Domain building blocks: entities with identity equality, aggregate roots, value objects, typed ids, business rules, domain events.
- Single pipeline with explicit ordering, validation that reports every failure, cancellation propagated end to end.
- Dispatcher with typed invokers cached per request type. No `dynamic`, no per-call reflection.
- EF Core unit of work: automatic commit, cascading domain event dispatch, single atomic save.
- ASP.NET Core module: `MapCommand` and `MapQuery` minimal API extensions, validation failures as RFC 7807 `ProblemDetails` with every field error, business rule violations as 422 responses.
- Source generators: compile-time handler discovery and dispatch priming, missing handlers reported at build time, duplicate handlers fail the build, Native AOT ready.
- Messaging: integration events with versioned JSON serialization, transactional outbox with retry and dead-letter, consumer dispatch with a unit of work per message, in-memory transport.
- Durable transports: Postgres (SKIP LOCKED queue with LISTEN/NOTIFY wake-up) and Redis (Streams with consumer groups), both with retry, dead-letter and competing consumers, configured in code or from environment variables.

---

## Next

### Jobs and queues

Background jobs built on the messaging infrastructure: enqueue from command handlers through the outbox, workers with retry and timeout, progress tracking over polling or push, and recurring schedules.

---

## Planned

| Module | Purpose |
|---|---|
| CLI (`truss`) | Project scaffolding, `truss new`, `truss add`, manifest-driven module installation |
| Auth | Pluggable authentication modules with a user model scaffolded into your domain, fully editable |
| Jobs and queues | Background jobs with progress tracking (streaming, websockets or polling), pluggable brokers, dashboard |
| Observability | Structured logging, OpenTelemetry, optional dashboards, enabled only if you want them |
| Mapping | Source-generated DTO mapping |

---

## Principles that will not change

- Every module is opt-in.
- Free and open-source options first.
- The domain layer stays dependency-free.
- No runtime magic.
