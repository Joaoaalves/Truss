# Truss

**Truss** is a modular .NET framework for building applications with **DDD**, **CQRS** and **Clean Architecture**, focused on developer experience, explicit behavior and minimal dependencies per layer.

Full documentation: **https://joaoaalves.github.io/Truss**

> Early development. APIs are subject to change until v1.

## Packages

Twenty-one packages in three rings plus tooling. A package boundary exists only
where it pays for itself: an external dependency it isolates or a deployment
target it serves.

| Package | Purpose | Ring |
|---|---|---|
| `Truss.Domain` | Entities, aggregate roots, value objects, typed ids, business rules, domain events. Zero dependencies. | Kernel |
| `Truss.Application` | Commands, queries, handlers, pipeline behaviors, dispatching, unit of work, plus the auth, tenancy and authorization contracts. | Kernel |
| `Truss.Messaging` | Integration events: versioned serialization, transactional outbox and inbox, transport seam, in-memory transport. | Capability |
| `Truss.Jobs` | Background jobs: transactional enqueueing, retry, timeout, live progress, scheduled and recurring jobs. | Capability |
| `Truss.Email` | The message shape, sender and validation contracts, and a console sender for development. | Capability |
| `Truss.Rbac` | Roles in code mapping to permissions, RequirePermission and claims enrichment. | Capability |
| `Truss.Observability` | Structured request logging, spans, metrics and ambient correlation. | Capability |
| `Truss.Remote` | Explicit synchronous queries to a context running as another service. | Capability |
| `Truss.Support` | The support surface of an application: wire contracts and the typed client for the Truss Deck. | Capability |
| `Truss.EntityFrameworkCore` | Unit of work, outbox and inbox stores, job store, tenancy interceptors, role assignments and idempotency, registered per feature. | Integration |
| `Truss.AspNetCore` | Endpoint mapping with ProblemDetails, remote context endpoints, job and outbox endpoints, correlation and tenant resolution. | Integration |
| `Truss.Messaging.Postgres` | Durable Postgres transport: SKIP LOCKED queue with LISTEN/NOTIFY wake-up. | Integration |
| `Truss.Messaging.RabbitMq` | Durable RabbitMQ transport: quorum queues with publisher confirms and broker-side dead-lettering. | Integration |
| `Truss.Messaging.Redis` | Durable Redis transport over Streams with consumer groups. | Integration |
| `Truss.Email.Smtp` | SMTP delivery through MailKit and DNS-checked address validation. | Integration |
| `Truss.Email.Resend` | Delivery through the Resend API behind the same sender abstraction. | Integration |
| `Truss.Auth.Jwt` | PBKDF2 hashing, JWT issuing and JwtBearer wiring; the user model is scaffolded into your domain. | Integration |
| `Truss.Observability.OpenTelemetry` | OTLP export of the framework's traces, metrics and logs in one registration. | Integration |
| `Truss.Generators` | Compile-time handler discovery and dispatch, build diagnostics, and DTO mappers generated from partial methods. | Tooling |
| `Truss.Testing` | Integration test host: pipeline, throwaway database, transport and jobs in one call. | Tooling |
| `Truss.Cli` | The `truss` command line: scaffolding, module installation, code generation, splitting and deploy artifacts. | Tooling |

## Principles

- **Explicit over implicit.** No hidden conventions, no runtime magic.
- **Minimal dependencies per layer.** The domain layer depends on nothing; the application layer never sees the ORM.
- **Developer experience first.** Commands validate themselves, the unit of work commits automatically, domain events dispatch at the right moment.
- **Failure transparency.** Exceptions propagate with their original stack trace.
