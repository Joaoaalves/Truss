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

## Principles

- **Explicit over implicit.** No hidden conventions, no runtime magic.
- **Minimal dependencies per layer.** The domain layer depends on nothing; the application layer never sees the ORM.
- **Developer experience first.** Commands validate themselves, the unit of work commits automatically, domain events dispatch at the right moment.
- **Failure transparency.** Exceptions propagate with their original stack trace.
