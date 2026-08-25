# Architecture

Truss assumes a Clean Architecture layout, but does not enforce one. Each package is designed for a specific layer, and the dependency direction always points inward.

---

## Layers and Packages

| Layer | Responsibility | Truss packages |
|---|---|---|
| Domain | Entities, value objects, domain events, business rules | `Truss.Domain` |
| Application | Commands, queries, handlers, validators | `Truss.Application.Abstractions` |
| Infrastructure | Persistence, unit of work implementation | `Truss.Persistence.EntityFrameworkCore` |
| API / Host | Registration, composition, endpoint mapping | `Truss.Application`, `Truss.AspNetCore` and modules |

The domain layer depends on nothing but `Truss.Domain`, which itself has zero dependencies. The application layer sees only abstractions: never EF Core, never a database driver.

---

## Namespaces

Truss uses flat namespaces so each layer needs a single using:

- `using Truss.Domain;` gives every domain building block.
- `using Truss.Application;` gives every application contract. The Abstractions package publishes into this namespace, following the .NET convention for `.Abstractions` packages.

Registration extensions (`AddTruss`, `AddTrussEntityFramework`) live in `Microsoft.Extensions.DependencyInjection`, so composition roots need no Truss usings at all.

---

## Execution Model

Every request, command or query, flows through the same pipeline. Behaviors wrap the handler in registration order, with the first registered behavior as the outermost:

```
IDispatcher.Send(request)
    ValidationBehavior        every request that has validators
        UnitOfWorkBehavior    commands only
            IRequestHandler
```

- The dispatcher resolves the handler and behaviors from the current dependency injection scope. In a web application, that is the request scope.
- The `CancellationToken` flows from the caller through every behavior into the handler.
- Dispatch uses a typed invoker cached per request type: reflection happens once per type, never per call.
- With the [Truss.Generators](generators.md) package installed, discovery and invokers are produced at compile time and no reflection runs at all.

---

## Commands vs Queries

| Concern | Commands | Queries |
|---|---|---|
| State changes | Yes | Never |
| Unit of work | Automatic commit | Not created |
| Domain events | Dispatched on commit | None |
| Validation | Yes | Yes, when validators exist |
| Handlers per message | Exactly one | Exactly one |

---

## Domain Events

Domain events are raised inside aggregates and dispatched by the unit of work inside the transactional boundary, before persistence. Handlers may modify other aggregates, and every change is committed atomically in a single save.

Side effects that must only happen after a successful commit, such as publishing to a message broker or sending e-mail, belong to [integration events](messaging.md): stored transactionally through the outbox and delivered by a background processor after the commit.

---

## The Extraction Path

A bounded context in Truss is a service waiting to be born, and the architecture is shaped so the birth is mechanical, never a rewrite.

The path has three explicit steps, each useful on its own:

1. **Folders**, the default: contexts live as folders inside the four layer projects, sharing one process and one database. Most applications live their whole lives here, well.
2. **Projects**: `truss g context --as-projects` gives a context its own Domain, Application and Infrastructure projects, with the compiler enforcing the layering. Namespaces do not change between the layouts, so moving an existing context moves files and nothing else.
3. **A service**: `truss split` gives the context its own host and, by default, its own database. Its routes move out of the monolith, its events keep flowing through the same outbox and transport, other services query it explicitly through [its contract](remote.md), and `truss dev` runs the whole constellation with one trace crossing every host.

What makes each step cheap is decided long before it happens: namespaces mirror folders and never change, repositories depend on `DbContext` rather than the application's concrete context, integration events are the default relationship between contexts, and everything a service exposes lives in a contracts project instead of its internals. The [deploy artifacts](deploy.md) then ship each host as its own image.
