# Unit of Work

Truss treats the unit of work as an execution concern, not an ORM abstraction. Its responsibility is to coordinate domain event dispatching and persistence in a deterministic way, and to keep both out of application code.

---

## The Contract

```csharp
public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
```

The abstraction is intentionally minimal: no transaction APIs, no ORM concepts, no lifecycle management. Application code never calls it. The pipeline does.

---

## Automatic Commit

`UnitOfWorkBehavior` wraps every command:

- When the handler succeeds, `CommitAsync` runs: domain events are dispatched, then all changes are persisted in a single save.
- When the handler throws, nothing is committed. The scoped context is discarded with the request and the exception propagates unchanged.

Queries never create or touch a unit of work.

---

## Domain Event Dispatching

The EF Core implementation collects domain events from every tracked entity and dispatches them before saving, in rounds:

1. Collect pending events from tracked entities and clear them.
2. Dispatch each event to its handlers.
3. Handlers may modify aggregates and raise new events. Collect and dispatch again.
4. When a round produces no new events, persist everything with a single `SaveChangesAsync`.

This gives domain event handlers a strong guarantee: whatever they change is committed atomically with the command. There is no window where the command persisted but its side effects inside the domain did not.

```csharp
public class OrderPlacedHandler(AppDbContext context) : IDomainEventHandler<OrderPlaced>
{
    public async Task Handle(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        var stock = await context.Stock.SingleAsync(s => s.OrderId == domainEvent.OrderId, cancellationToken);
        stock.Reserve();   // persisted in the same save as the order
    }
}
```

---

## What a Domain Event Handler Can See

Domain events are dispatched inside the transactional boundary, before `SaveChanges`. That guarantee has a consequence worth spelling out: a LINQ query goes to the database, and the aggregate that raised the event is not there yet. A handler that looks its own aggregate up by query gets null, silently:

```csharp
// Wrong: the aggregate is still untracked rows-to-be
var food = await context.Foods.SingleOrDefaultAsync(f => f.Barcode == domainEvent.Barcode);

// Right: FindAsync consults the change tracker before the database
var food = await context.Foods.FindAsync(domainEvent.Id);
```

Reading *other* aggregates by query is fine, which is exactly why the trap hides: examples that check stock or counts against a different aggregate work, and the one time the handler reaches for its own aggregate, the outbox stays quiet and nothing explains why. Reach for the raising aggregate by key, or carry what the handler needs on the event itself.

---

## Failure Scenarios

| Scenario | Outcome |
|---|---|
| Validation fails | Handler never runs, unit of work never created |
| Handler throws | No commit, no events dispatched, exception propagates |
| Domain event handler throws | No commit, the whole command fails atomically |
| `SaveChangesAsync` throws | Events were dispatched in-memory, but nothing was persisted; exception propagates |

Because domain event handlers run inside the transactional boundary, a failing handler fails the command, by design. Side effects that must not roll back with the command (e-mails, broker messages) belong to [integration events](messaging.md), published through the outbox so they are stored atomically with the command and delivered after the commit.

---

## EF Core Registration

```csharp
services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
services.AddTrussEntityFramework<AppDbContext>();
```

`AddTrussEntityFramework<TDbContext>` registers:

- `IUnitOfWork` backed by `EfUnitOfWork<TDbContext>`
- The `UnitOfWorkBehavior` pipeline registration

The unit of work is bound to a specific context type. In a modular monolith, each bounded context can own its `DbContext` without collisions.

---

## Architectural Guarantees

| Concern | Guarantee |
|---|---|
| Transaction scope | One per command |
| Queries | No unit of work |
| Automatic commit | Yes |
| Domain event consistency | Atomic with the command |
| Cascading events | Dispatched until exhausted, single save |
| Exception transparency | Guaranteed |
