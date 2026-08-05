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
- Durable transports: Postgres (SKIP LOCKED queue with LISTEN/NOTIFY wake-up), RabbitMQ (quorum queues with publisher confirms and broker-side dead-lettering) and Redis (Streams with consumer groups), all with retry, dead-letter and competing consumers, configured in code or from environment variables.
- Background jobs: transactional enqueueing through the outbox, retry and timeout per attempt, live progress with polling and server-sent events endpoints, scheduled and cron-recurring jobs.
- Observability: structured logging of every request with outcome-aware levels, correlation ids flowing from HTTP to handlers, spans for requests, messages and jobs, request metrics, all through BCL diagnostics with no exporter dependency.
- The `truss` CLI: interactive scaffolding with database and docker choices, a manifest that lets modules be installed months after the project started, generators for aggregates, commands and queries, and a doctor that verifies the project against the manifest.
- Compile-time DTO mapping: mappers generated from partial method declarations, typed id unwrapping, nested and collection mapping, computed members through named methods, unmapped members as build errors.
- Authentication: JWT mechanics in packages (PBKDF2 hashing, access and refresh tokens with rotation, JwtBearer wiring) with the user model, account commands and repositories scaffolded into your own layers by `truss add auth`, fully editable.

---

- First release: every package and the CLI published to nuget.org as 0.1.0 through NuGet trusted publishing.
- RabbitMQ transport and outbox retention: processed messages are cleaned up after a configurable period.
- Jobs hardening: exponential retry backoff, cancellation of queued and running jobs, retention of finished records, and lease-based scheduler locking so every instance can keep the schedulers enabled.

---

## Next

### Auth providers

ASP.NET Core Identity integration and external OpenID providers over the same scaffolded, editable account model.

---

## Planned

| Module | Purpose |
|---|---|
| Auth providers | ASP.NET Core Identity integration and external OpenID providers |
| Dashboards | Generated compose files for log, trace and job dashboards |

---

## Principles that will not change

- Every module is opt-in.
- Free and open-source options first.
- The domain layer stays dependency-free.
- No runtime magic.
