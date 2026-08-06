# Truss

**Truss** is a modular .NET framework for building applications with **DDD**, **CQRS** and **Clean Architecture**, focused on developer experience, explicit behavior and minimal dependencies per layer.

Full documentation: **https://joaoaalves.github.io/Truss**

> Early development. APIs are subject to change until v1.

## Packages

| Package | Purpose | Layer |
|---|---|---|
| `Truss.Domain` | Entities, aggregate roots, value objects, typed ids, business rules, domain events. Zero dependencies. | Domain |
| `Truss.Application.Abstractions` | Contracts for commands, queries, handlers, pipeline behaviors and unit of work. | Application |
| `Truss.Application` | Dispatcher, validation pipeline and handler registration. | Application / Composition root |
| `Truss.Persistence.EntityFrameworkCore` | EF Core unit of work with automatic commit and domain event dispatching. | Infrastructure |
| `Truss.AspNetCore` | Endpoint mapping for commands and queries with automatic ProblemDetails responses. | API / Host |
| `Truss.Generators` | Compile-time handler discovery and dispatch, missing handlers as build diagnostics. | Build (dev dependency) |
| `Truss.Messaging.Abstractions` | Contracts for integration events, handlers and the publisher. | Application |
| `Truss.Messaging` | Versioned JSON serialization, outbox runtime, transport seam, in-memory transport. | Infrastructure |
| `Truss.Messaging.EntityFrameworkCore` | Transactional outbox stored through EF Core. | Infrastructure |
| `Truss.Messaging.Postgres` | Durable Postgres transport: SKIP LOCKED queue with LISTEN/NOTIFY wake-up. | Infrastructure |
| `Truss.Messaging.RabbitMq` | Durable RabbitMQ transport: quorum queues with publisher confirms and broker-side dead-lettering. | Infrastructure |
| `Truss.Messaging.Redis` | Durable Redis transport over Streams with consumer groups. | Infrastructure |
| `Truss.Jobs.Abstractions` | Contracts for background jobs, scheduling and monitoring. | Application |
| `Truss.Jobs` | Job runtime: transactional enqueueing, retry, timeout, scheduled and recurring jobs. | Infrastructure |
| `Truss.Jobs.EntityFrameworkCore` | Job records persisted in the application database. | Infrastructure |
| `Truss.Jobs.AspNetCore` | Progress endpoints: polling and server-sent events. | API / Host |
| `Truss.Observability` | Structured request logging, spans, metrics and ambient correlation. | Cross-cutting |
| `Truss.Observability.AspNetCore` | Correlation middleware bridging HTTP headers. | API / Host |
| `Truss.Observability.OpenTelemetry` | OTLP export of the framework's traces, metrics and logs in one registration. | API / Host |
| `Truss.Testing` | Integration test host: pipeline, throwaway database, transport and jobs in one call. | Tests |
| `Truss.Cli` | The `truss` command line: scaffolding, module installation, code generation. | Tooling (dotnet tool) |
| `Truss.Mapping` | Compile-time DTO mappers with typed id unwrapping and unmapped members as build errors. | Build (dev dependency) |
| `Truss.Auth.Abstractions` | Contracts for password hashing and token issuing. | Application |
| `Truss.Auth.Jwt` | PBKDF2 hashing, JWT issuing and JwtBearer wiring; the user model is scaffolded into your domain. | API / Host |
| `Truss.Email.Abstractions` | Contracts for sending email from the application layer. | Application |
| `Truss.Email` | SMTP sender through MailKit and a console sender for development. | API / Host |

## Principles

- **Explicit over implicit.** No hidden conventions, no runtime magic.
- **Minimal dependencies per layer.** The domain layer depends on nothing; the application layer never sees the ORM.
- **Developer experience first.** Commands validate themselves, the unit of work commits automatically, domain events dispatch at the right moment.
- **Failure transparency.** Exceptions propagate with their original stack trace.
