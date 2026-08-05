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
