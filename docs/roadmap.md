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
- Worker template: truss add worker scaffolds a separate consumer process sharing the application layers, competing for messages and jobs with the API.
- Error catalog: business rules carry a stable Code surfaced in 422 responses, overridable to survive renames.
- Seeders: ITrussSeeder classes plant development data after the schema is ready; the sample ships one.
- Email: IEmailSender for the application layer with SMTP and console providers, Mailpit wired into development by truss add email, and address validation combining the RFC parser with a DNS deliverability answer.
- Account flows: password reset, email confirmation and two factor login by email scaffolded with auth when the email module is present, on single-use hashed tokens consumed atomically.
- Resend provider: truss add email --provider resend delivers through the official API client behind the same sender abstraction.
- Multi-tenancy: row-level isolation with a clean domain, ambient resolution and loud failure on unstamped writes.
- RBAC: roles in code mapping to permissions, RequirePermission on any endpoint and assignments in the database, independent of tenancy.
- truss update: every Truss package pointed at the CLI's version in one command.
- Database per tenant: one registered mapping routes each tenant to its own database, coexisting with the shared default.
- Tenant-scoped grants: role assignments that apply inside one tenant, resolved against the ambient tenant automatically.
- Rich generators: aggregates in their own folder with value objects, events and a starter rule; a new entity generator; and --crud producing the whole vertical slice with routes wired.
- User binding: truss add auth --bind-user connects the account to an existing aggregate, holding its id (reference) or being it outright (merge).
- External login providers: Google, Microsoft and GitHub OAuth combinable with either credential provider, resolved into the same editable account model.
- Clean scaffold by default: truss new creates no example code unless --sample asks for the Catalog context, and truss remove context deletes a bounded context with all its wiring.
- Scaffolded tests: every new project carries a domain test project and an integration test project on the TrussTestHost, and generators add matching tests, so generated code arrives tested.
- Mirrored namespaces: generated code's namespaces follow the folders exactly, with each command and query in its own folder and DTOs and rules in theirs. The sample context and everything truss add auth scaffolds follow the same layout.
- Value objects with invariants: --vo on aggregates and entities wraps every primitive in a self-validating value object with its rules, tests and EF conversion, and truss g vo builds shared multi-member ones. Short aliases for every generator and its frequent options.
- Rule segments and composition: inclusive ranges and REST-style comparators on any --vo member (Name:string:3..120, Calories:int:0..900, pos), composite value objects built from members that guard themselves, -a to place one inside its owning aggregate, and references to existing value objects by name.
- Fewer first-hour surprises: the scaffold pins the solution for editors, and truss update restores without the http cache so a version published minutes ago is not reported as missing.

---

## Next

Further providers (login and transactional email) land by demand.

---

## Principles that will not change

- Every module is opt-in.
- Free and open-source options first.
- The domain layer stays dependency-free.
- No runtime magic.
