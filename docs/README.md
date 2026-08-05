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

Each layer references only the packages it needs. See [Architecture](architecture.md) for the full picture.

---

## Status

Truss is in early development. The kernel is implemented and tested: domain building blocks, the dispatching pipeline, validation and the unit of work. See the [Roadmap](roadmap.md) for what comes next.
