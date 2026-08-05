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

| Package | Purpose | Layer |
|---|---|---|
| `Truss.Domain` | Entities, aggregate roots, value objects, typed ids, business rules, domain events. Zero dependencies. | Domain |
| `Truss.Application.Abstractions` | Contracts for commands, queries, handlers, pipeline behaviors, dispatching and unit of work. | Application |
| `Truss.Application` | Dispatcher, validation pipeline, handler registration. | Application / Composition root |
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
| `Truss.Cli` | The `truss` command line: scaffolding, module installation, code generation. | Tooling (dotnet tool) |
| `Truss.Mapping` | Compile-time DTO mappers with typed id unwrapping and unmapped members as build errors. | Build (dev dependency) |
| `Truss.Auth.Abstractions` | Contracts for password hashing and token issuing. | Application |
| `Truss.Auth.Jwt` | PBKDF2 hashing, JWT issuing and JwtBearer wiring; the user model is scaffolded into your domain. | API / Host |

Each layer references only the packages it needs. See [Architecture](architecture.md) for the full picture.

---

## For AI Assistants

This documentation is AI-friendly: [llms.txt](https://joaoaalves.github.io/Truss/llms.txt) is a curated index describing when to consult each page, [llms-full.txt](https://joaoaalves.github.io/Truss/llms-full.txt) is the whole documentation in a single file, and every page is fetchable as plain Markdown at its .md URL.

---

## Status

Truss 0.1.x is published on [nuget.org](https://www.nuget.org/packages?q=Truss.): the domain building blocks, the pipeline, persistence, messaging with outbox and durable transports, background jobs with live progress, observability, authentication, the source generators and the CLI. APIs may still change until v1. See the [Roadmap](roadmap.md) for what comes next.
