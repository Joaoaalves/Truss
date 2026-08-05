# Dependencies

This section lists the **direct dependencies added to a project** when referencing each Truss package.

---

## Truss.Domain

**Purpose:**
Domain building blocks: entities, aggregate roots, value objects, typed identifiers, business rules and domain events.

### Dependencies

None. `Truss.Domain` relies exclusively on the .NET runtime, making it safe to reference from any domain layer or shared kernel.

---

## Truss.Application.Abstractions

**Purpose:**
Contracts for commands, queries, handlers, pipeline behaviors, dispatching, validation errors and unit of work.

### Dependencies

- `Truss.Domain`

---

## Truss.Application

**Purpose:**
The execution pipeline: dispatcher, domain event dispatcher, validation behavior, unit of work behavior and handler registration.

### Dependencies

- `Truss.Application.Abstractions`
- `FluentValidation`
- `Microsoft.Extensions.DependencyInjection.Abstractions`

---

## Truss.Persistence.EntityFrameworkCore

**Purpose:**
EF Core implementation of the unit of work, with domain event collection from tracked entities.

### Dependencies

- `Truss.Application`
- `Microsoft.EntityFrameworkCore`

---

## Truss.AspNetCore

**Purpose:**
Endpoint mapping for commands and queries, with automatic ProblemDetails responses for validation and business rule failures.

### Dependencies

- `Truss.Application.Abstractions`
- ASP.NET Core shared framework (framework reference, not a package)

---

## Truss.Generators

**Purpose:**
Compile-time handler discovery, dispatch priming and build diagnostics. Development dependency: nothing is added to the published application.

### Dependencies

None at runtime. Referenced with `PrivateAssets="all"` in the composition root project.

---

## Truss.Messaging.Abstractions

**Purpose:**
Contracts for integration events: the event marker and base record, the handler, the publisher and the naming attribute with versioning.

### Dependencies

None.

---

## Truss.Messaging

**Purpose:**
Integration event runtime: versioned JSON serialization, outbox model and processor, transport abstraction, consumer dispatch and the in-memory transport.

### Dependencies

- `Truss.Messaging.Abstractions`
- `Truss.Application.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Hosting.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Options`

---

## Truss.Messaging.EntityFrameworkCore

**Purpose:**
Outbox persistence through EF Core: the model configuration and a store that joins the command's unit of work.

### Dependencies

- `Truss.Messaging`
- `Microsoft.EntityFrameworkCore.Relational`

---

## Truss.Messaging.Postgres

**Purpose:**
Durable Postgres transport: table-backed queue with SKIP LOCKED competing consumers, LISTEN/NOTIFY wake-up, retry with backoff and a dead-letter table.

### Dependencies

- `Truss.Messaging`
- `Npgsql`

---

## Truss.Messaging.Redis

**Purpose:**
Durable Redis transport: Streams with consumer groups, pending message reclaim and a dead-letter stream.

### Dependencies

- `Truss.Messaging`
- `StackExchange.Redis`
