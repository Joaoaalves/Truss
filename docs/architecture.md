# Architecture

Truss assumes a **Clean Architecture** layout, but does not enforce one. Each package is designed for a specific layer, and the dependency direction always points inward.

---

## Layers and Packages

```
API / Host              →  Truss.Application, infrastructure modules (composition root)
Infrastructure          →  Truss.Persistence.EntityFrameworkCore
Application             →  Truss.Application.Abstractions
Domain                  →  Truss.Domain
```

| Layer | Responsibility | Truss packages |
|---|---|---|
| Domain | Entities, value objects, domain events, business rules | `Truss.Domain` |
| Application | Commands, queries, handlers, validators | `Truss.Application.Abstractions` |
| Infrastructure | Persistence, unit of work implementation | `Truss.Persistence.EntityFrameworkCore` |
| API / Host | Registration and composition | `Truss.Application` + modules |

The domain layer depends on nothing but `Truss.Domain`, which itself has **zero dependencies**. The application layer sees only abstractions — never EF Core, never a database driver.

---

## Namespaces

Truss uses **flat namespaces** so each layer needs a single using:

- `using Truss.Domain;` — every domain building block
- `using Truss.Application;` — every application contract (published by the Abstractions package, following the .NET convention for `.Abstractions` packages)

Registration extensions (`AddTruss`, `AddTrussEntityFramework`) live in `Microsoft.Extensions.DependencyInjection`, so composition roots need no Truss usings at all.

---

## Execution Model

Every request — command or query — flows through the same pipeline:

```
IDispatcher.Send(request)
  └─ behaviors (registration order, first = outermost)
      └─ ValidationBehavior        (all requests with validators)
          └─ UnitOfWorkBehavior    (commands only)
              └─ IRequestHandler
```

- The dispatcher resolves the handler and behaviors from the **current dependency injection scope** — in a web application, that is the request scope
- The `CancellationToken` flows from the caller through every behavior into the handler
- Dispatch uses a typed invoker cached per request type: reflection happens once per type, never per call

---

## Commands vs Queries

| Concern | Commands | Queries |
|---|---|---|
| State changes | Yes | Never |
| Unit of work | Automatic commit | Not created |
| Domain events | Dispatched on commit | None |
| Validation | Yes | Yes (when validators exist) |
| Handlers per message | Exactly one | Exactly one |

---

## Domain Events

Domain events are raised inside aggregates and dispatched by the unit of work **inside the transactional boundary, before persistence**. Handlers may modify other aggregates, and every change is committed atomically in a single save.

Side effects that must only happen after a successful commit — publishing to a message broker, sending e-mail — belong to **integration events**, a separate concept planned for the messaging module (see [Roadmap](roadmap.md)).
