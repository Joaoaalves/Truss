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
- Dashboards: the OpenTelemetry bridge package and one-command wiring of the Aspire dashboard, Grafana or Seq through the CLI, with the compose service and the development environment configured.
- AGENTS.md: every scaffolded project carries agent guidance regenerated from the manifest, with user content preserved outside the managed markers.
- Identity provider: truss add auth --provider identity runs the credential mechanics through ASP.NET Core Identity over the same clean domain model.
- Truss.Testing: an integration test host booting the pipeline, a throwaway database, the in-memory transport and the job runtime in one call.
- truss dev: docker dependencies, the URLs that matter and the API under hot reload in one command; scaffolds now serve an interactive API reference at /scalar in development.
- Health checks: each module reports its own health through the standard ASP.NET Core checks, with counters in the response data; scaffolds map /health from the start.
- Migrations: truss db add and truss db migrate wrap dotnet-ef through a scaffolded tool manifest, and development startup applies pending migrations automatically once migrations exist.
- Pagination: PageRequest and PageResult contracts, ToPageAsync over any queryable, query string binding through MapQuery and a --paged flag on the query generator.
- Idempotent commands: the Idempotency-Key header replays stored responses instead of re-executing, with the record committed in the same transaction as the command.

---

## Next

### Auth providers

External OpenID providers (Google, Microsoft, GitHub) and scaffolded password reset and email confirmation flows over the same editable account model.

---

## Planned

| Module | Purpose |
|---|---|
| Auth providers | External OpenID providers and scaffolded account flows |

---

## Principles that will not change

- Every module is opt-in.
- Free and open-source options first.
- The domain layer stays dependency-free.
- No runtime magic.
