# Commands & Queries

Truss distinguishes explicitly between:

- **Commands** — state-changing operations, executed inside a unit of work
- **Queries** — read-only operations, with no transactional participation
- **Domain event handlers** — reactions to something that happened in the domain

All contracts live in the `Truss.Application` namespace.

---

## Commands

A command is a record implementing `ICommand<TResult>` — or `ICommand` when there is nothing to return:

```csharp
public sealed record CreateUser(string Name, string Email) : ICommand<Guid>;

public sealed record DeactivateUser(Guid UserId) : ICommand;
```

Each command has **exactly one** handler:

```csharp
public class CreateUserHandler(AppDbContext context) : ICommandHandler<CreateUser, Guid>
{
    public Task<Guid> Handle(CreateUser command, CancellationToken cancellationToken)
    {
        var user = User.Create(command.Name, command.Email);
        context.Users.Add(user);
        return Task.FromResult(user.Id.Value);
    }
}
```

Command handlers:

- Never call `SaveChangesAsync` — the unit of work commits automatically
- Never dispatch domain events — the unit of work does
- Should return ids or DTOs, never entities

Commands with no result return `Unit`:

```csharp
public class DeactivateUserHandler(AppDbContext context) : ICommandHandler<DeactivateUser>
{
    public async Task<Unit> Handle(DeactivateUser command, CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([command.UserId], cancellationToken);
        user!.Deactivate();
        return Unit.Value;
    }
}
```

---

## Queries

A query implements `IQuery<TResult>` and never changes state:

```csharp
public sealed record GetUserById(Guid UserId) : IQuery<UserDto?>;

public class GetUserByIdHandler(AppDbContext context) : IQueryHandler<GetUserById, UserDto?>
{
    public Task<UserDto?> Handle(GetUserById query, CancellationToken cancellationToken)
    {
        return context.Users
            .Where(u => u.Id == new UserId(query.UserId))
            .Select(u => new UserDto(u.Id.Value, u.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

Queries do not create a unit of work and never trigger a commit — reading is free of transactional overhead.

---

## Dispatching

`IDispatcher` is the single entry point for commands and queries:

```csharp
public class UsersController(IDispatcher dispatcher)
{
    public Task<Guid> Create(CreateUser command, CancellationToken ct)
        => dispatcher.Send(command, ct);
}
```

Dispatch characteristics:

- Handlers and behaviors are resolved from the **current scope** — the same `DbContext` your request already uses
- The `CancellationToken` is propagated end to end
- A missing handler fails immediately with a clear exception
- Reflection happens **once per request type**; subsequent dispatches are strongly-typed calls on a cached invoker

---

## Domain Event Handlers

React to domain events by implementing `IDomainEventHandler<TEvent>`:

```csharp
public class OrderPlacedHandler(AppDbContext context) : IDomainEventHandler<OrderPlaced>
{
    public async Task Handle(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        var summary = await context.DailySummaries.SingleAsync(cancellationToken);
        summary.RegisterOrder(domainEvent.OrderId);
    }
}
```

Handlers run **inside the unit of work**, before persistence — any state they modify is committed atomically with the command. Zero or more handlers may exist per event. See [Unit of Work](unit-of-work.md) for the full dispatch semantics.

---

## Registration

All handlers are discovered from the assemblies registered in `AddTruss`:

```csharp
services.AddTruss(options =>
{
    options.AddAssembly<CreateUser>();
});
```

Using a marker type ensures the correct assembly is scanned. Registration is explicit — assemblies are never discovered implicitly.
