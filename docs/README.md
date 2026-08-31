# Introduction

**Truss** is a modular .NET framework for building applications with **Domain-Driven Design**, **CQRS** and **Clean Architecture**.

In structural engineering, a truss is a frame of connected members that only carries load as a whole. Truss applies the same idea to software: small, explicit building blocks that compose into a solid structure, so application code can focus on the domain.

The framework carries the infrastructure weight for you:

- Commands validate themselves. Every failure is collected and reported at once.
- The unit of work commits itself. Handlers never touch transactions.
- Domain events are dispatched at the right moment: inside the transactional boundary, before persistence.
- Failures are transparent. Exceptions propagate with their original stack trace.

---

## Design Intent

Truss is intentionally explicit, modular and dependency-minimal.

- **Explicit over implicit.** No hidden conventions and no implicit discovery. Assemblies are registered explicitly and behaviors execute in a documented order.
- **Minimal dependencies per layer.** The domain layer depends on nothing. The application layer never sees the ORM. Each package is referenced only by the layer that needs it.
- **No runtime magic.** Dispatch uses a typed invoker cached per request type. There is no `dynamic` and no per-call reflection.
- **Developer experience first.** The framework removes boilerplate without removing control.

---

## Packages

Twenty-one packages in three rings plus tooling. A package boundary exists only
where it pays for itself: an external dependency it isolates or a deployment
target it serves. Everything else is a namespace.

**Kernel** - referenced by domain and application code, no external weight:

| Package | Purpose |
|---|---|
| `Truss.Domain` | Entities, aggregate roots, value objects, typed ids, business rules, domain events. Zero dependencies. |
| `Truss.Application` | Commands, queries, handlers, pipeline behaviors, dispatching, unit of work, plus the auth, tenancy and authorization contracts. |

**Capabilities** - feature runtimes, `Microsoft.Extensions.*`-class dependencies only:

| Package | Purpose |
|---|---|
| `Truss.Messaging` | Integration events: versioned serialization, transactional outbox and inbox, transport seam, in-memory transport. |
| `Truss.Jobs` | Background jobs: transactional enqueueing, retry, timeout, live progress, scheduled and recurring jobs. |
| `Truss.Email` | The message shape, sender and validation contracts, and a console sender for development. |
| `Truss.Rbac` | Roles in code mapping to permissions, RequirePermission and claims enrichment. |
| `Truss.Observability` | Structured request logging, spans, metrics and ambient correlation. |
| `Truss.Remote` | Explicit synchronous queries to a context running as another service. |
| `Truss.Support` | The support surface of an application: wire contracts and the typed client for the Truss Deck. |

**Integrations** - real third-party dependencies, referenced only by hosts:

| Package | Purpose |
|---|---|
| `Truss.EntityFrameworkCore` | Unit of work, outbox and inbox stores, job store, tenancy interceptors, role assignments and idempotency, registered per feature. |
| `Truss.AspNetCore` | Endpoint mapping with ProblemDetails, remote context endpoints, job and outbox endpoints, correlation and tenant resolution. |
| `Truss.Messaging.Postgres` | Durable Postgres transport: SKIP LOCKED queue with LISTEN/NOTIFY wake-up. |
| `Truss.Messaging.RabbitMq` | Durable RabbitMQ transport: quorum queues with publisher confirms and broker-side dead-lettering. |
| `Truss.Messaging.Redis` | Durable Redis transport over Streams with consumer groups. |
| `Truss.Email.Smtp` | SMTP delivery through MailKit and DNS-checked address validation. |
| `Truss.Email.Resend` | Delivery through the Resend API behind the same sender abstraction. |
| `Truss.Auth.Jwt` | PBKDF2 hashing, JWT issuing and JwtBearer wiring; the user model is scaffolded into your domain. |
| `Truss.Observability.OpenTelemetry` | OTLP export of the framework's traces, metrics and logs in one registration. |

**Tooling** - never shipped inside the application:

| Package | Purpose |
|---|---|
| `Truss.Generators` | Compile-time handler discovery and dispatch, missing handlers as build diagnostics, and DTO mappers generated from partial methods. |
| `Truss.Testing` | Integration test host: pipeline, throwaway database, transport and jobs in one call. |
| `Truss.Cli` | The `truss` command line: scaffolding, module installation, code generation, splitting and deploy artifacts. |

Each layer references only the packages it needs. See [Architecture](architecture.md) for the full picture.

---

## For AI Assistants

This documentation is AI-friendly: [llms.txt](https://joaoaalves.github.io/Truss/llms.txt) is a curated index describing when to consult each page, [llms-full.txt](https://joaoaalves.github.io/Truss/llms-full.txt) is the whole documentation in a single file, and every page is fetchable as plain Markdown at its .md URL.

---

## Status

Truss 0.5.x is published on [nuget.org](https://www.nuget.org/packages?q=Truss.): the domain building blocks, the pipeline, persistence, messaging with outbox and durable transports, background jobs with live progress, observability, authentication, service splitting with remote contexts, deploy artifacts, the source generators and the CLI. APIs may still change until v1. See the [Roadmap](roadmap.md) for what comes next.
