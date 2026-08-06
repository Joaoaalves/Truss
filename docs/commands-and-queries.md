# Commands & Queries

Truss distinguishes explicitly between:

- **Commands**: state-changing operations, executed inside a unit of work.
- **Queries**: read-only operations, with no transactional participation.
- **Domain event handlers**: reactions to something that happened in the domain.

All contracts live in the `Truss.Application` namespace.

---

## Commands

A command is a record implementing `ICommand<TResult>`, or `ICommand` when there is nothing to return:

```csharp
public sealed record CreateUser(string Name, string Email) : ICommand<Guid>;

public sealed record DeactivateUser(Guid UserId) : ICommand;
```

Each command has exactly one handler:

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

- Never call `SaveChangesAsync`. The unit of work commits automatically.
- Never dispatch domain events. The unit of work does.
- Should return ids or DTOs, never entities.

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

Queries do not create a unit of work and never trigger a commit. Reading is free of transactional overhead.

### Pagination

Paged queries follow one convention: `Page` and `Size` properties on the query, `PageResult<T>` as the result. The properties bind straight from the query string through `MapQuery`, and the EF package pages any queryable in one call:

```csharp
public sealed record ListProducts(int Page = 1, int Size = 20) : IQuery<PageResult<ProductDto>>;

public class ListProductsHandler(AppDbContext context) : IQueryHandler<ListProducts, PageResult<ProductDto>>
{
    public Task<PageResult<ProductDto>> Handle(ListProducts query, CancellationToken cancellationToken)
    {
        return context.Products
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id.Value, p.Name))
            .ToPageAsync(new PageRequest(query.Page, query.Size), cancellationToken);
    }
}
```

`GET /products?page=2&size=20` returns the items plus `totalCount`, `totalPages`, `hasPreviousPage` and `hasNextPage`, everything a client pager needs. Order the query before paging; an unordered skip has no stable meaning. Limits are the validator's job, and `truss generate query --paged` scaffolds the whole convention: the record, the handler pointing at `ToPageAsync` and a validator capping the size.

---

## Idempotent Commands

A client that times out and retries a `POST` should not create the order twice. Truss makes commands idempotent per client-supplied key with three registrations:

```csharp
builder.Services.AddTrussIdempotency<AppDbContext>();   // after AddTrussEntityFramework

app.UseTrussIdempotency();                              // reads the Idempotency-Key header

modelBuilder.ApplyTrussIdempotency();                   // in OnModelCreating
```

When a request carries an `Idempotency-Key` header, the pipeline checks the store before the handler: a known key returns the stored response without executing anything; a new key executes normally and the response record commits **in the same transaction as the command**. That atomicity is the point: a command can never apply twice, and a failed command leaves no record, so the retry genuinely runs.

Semantics worth knowing:

- The key is scoped per command type; reusing a key on a different command never replays a foreign response.
- Two concurrent requests racing the same key resolve at the database: one commit wins, the other rolls back entirely and surfaces an error; that client's retry replays the winner's response.
- Requests without the header are untouched, and queries ignore the key.
- Stored responses expire after 24 hours by default (`Truss__Idempotency__RetentionPeriod`), swept in the background.

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

- Handlers and behaviors are resolved from the current scope, so they share the same `DbContext` your request already uses.
- The `CancellationToken` is propagated end to end.
- A missing handler fails immediately with a clear exception.
- Reflection happens once per request type. Subsequent dispatches are strongly-typed calls on a cached invoker.

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

Handlers run inside the unit of work, before persistence. Any state they modify is committed atomically with the command. Zero or more handlers may exist per event. See [Unit of Work](unit-of-work.md) for the full dispatch semantics.

---

## Registration

All handlers are discovered from the assemblies registered in `AddTruss`:

```csharp
services.AddTruss(options =>
{
    options.AddAssembly<CreateUser>();
});
```

Using a marker type ensures the correct assembly is scanned. Registration is explicit. Assemblies are never discovered implicitly.
