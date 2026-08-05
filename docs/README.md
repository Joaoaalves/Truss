# Introduction

**Truss** is a modular .NET framework for building applications with **Domain-Driven Design**, **CQRS** and **Clean Architecture**.

A truss is a structure of members that only works as a whole — each part carries load so the others don't have to. Truss applies the same idea to application architecture: small, explicit building blocks that compose into a solid structure, so your code can focus on the domain.

The framework carries the infrastructure weight for you:

- **Commands validate themselves** — every validation failure is collected and reported at once
- **The unit of work commits itself** — handlers never touch transactions
- **Domain events dispatch at the right moment** — inside the transactional boundary, before persistence
- **Failures are transparent** — exceptions propagate with their original stack trace

---

## Design Intent

Truss is intentionally **explicit, modular and dependency-minimal**.

- **Explicit over implicit** — no hidden conventions, no implicit discovery. Assemblies are registered explicitly; behaviors execute in a documented order.
- **Minimal dependencies per layer** — the domain layer depends on nothing. The application layer never sees the ORM. Each package is referenced only by the layer that needs it.
- **No runtime magic** — dispatch uses a typed invoker cached per request type. No `dynamic`, no per-call reflection.
- **Developer experience first** — the framework removes boilerplate without removing control.

---

## Packages

| Package | Purpose | Layer |
|---|---|---|
| `Truss.Domain` | Entities, aggregate roots, value objects, typed ids, business rules, domain events. Zero dependencies. | Domain |
| `Truss.Application.Abstractions` | Contracts for commands, queries, handlers, pipeline behaviors, dispatching and unit of work. | Application |
| `Truss.Application` | Dispatcher, validation pipeline, handler registration. | Application / Composition root |
| `Truss.Persistence.EntityFrameworkCore` | EF Core unit of work with automatic commit and domain event dispatching. | Infrastructure |

Each layer references **only the packages it needs**. See [Architecture](architecture.md) for the full picture.

---

## Status

Truss is in early development. The kernel — domain building blocks, dispatching pipeline, validation and unit of work — is implemented and tested. See the [Roadmap](roadmap.md) for what comes next.
