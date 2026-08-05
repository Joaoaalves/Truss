# Messaging

`Truss.Messaging` carries events across the boundary of the application or module that produced them. It separates two concepts that are often conflated:

| Concept | Scope | Timing | Failure behavior |
|---|---|---|---|
| Domain events | Inside the service, same transaction | Before persistence | A failing handler fails the command |
| Integration events | Across services or modules | After the commit | Retried; never rolls back the command |

Domain events belong to the [unit of work](unit-of-work.md). This page is about integration events.

---

## Defining Integration Events

An integration event is a record with a stable wire name and version:

```csharp
[IntegrationEventName("orders.order-placed")]
public sealed record OrderPlaced(Guid OrderId, decimal Total) : IntegrationEvent;
```

The name travels with the serialized event, so renaming the CLR type never breaks consumers. Without the attribute, the full CLR type name is used with version 1; the attribute is recommended for anything that crosses a service boundary.

### Versioning

When the payload shape changes, declare a new type with the same name and a higher version:

```csharp
[IntegrationEventName("orders.order-placed", Version = 2)]
public sealed record OrderPlacedV2(Guid OrderId, decimal Total, string Currency) : IntegrationEvent;
```

Events are serialized as JSON inside an envelope carrying the name and version. On the consumer side, the pair resolves to the registered CLR type, so version 1 and version 2 messages coexist, each handled by its own handler. An unknown name or version fails with a clear exception instead of guessing.

---

## Publishing with the Outbox

Inject `IIntegrationEventPublisher` and publish from a command handler:

```csharp
public class PlaceOrderHandler(AppDbContext context, IIntegrationEventPublisher publisher)
    : ICommandHandler<PlaceOrder, Guid>
{
    public async Task<Guid> Handle(PlaceOrder command, CancellationToken cancellationToken)
    {
        var order = Order.Place(command.Items);
        context.Orders.Add(order);

        await publisher.Publish(new OrderPlaced(order.Id.Value, order.Total), cancellationToken);

        return order.Id.Value;
    }
}
```

With the outbox configured, `Publish` does not touch the broker. The event is stored in the outbox table by the same atomic save that persists the order: if the command fails, no event exists; if it commits, delivery is guaranteed. A background processor publishes stored messages to the transport with exponential backoff on failure, and dead-letters a message after the attempt limit, preserving the error.

Delivery is **at-least-once**: consumers must be idempotent.

---

## Consuming

Implement `IIntegrationEventHandler<TEvent>`:

```csharp
public class OrderPlacedHandler(BillingDbContext context) : IIntegrationEventHandler<OrderPlaced>
{
    public async Task Handle(OrderPlaced integrationEvent, CancellationToken cancellationToken)
    {
        context.Invoices.Add(Invoice.For(integrationEvent.OrderId, integrationEvent.Total));
    }
}
```

Each message is handled in its own dependency injection scope. When a unit of work is registered, it commits after the handlers succeed, so everything a handler changes is atomic per message. A failing handler propagates to the transport, which decides retry semantics.

---

## Transports

`IMessageTransport` is the seam between Truss and the broker. Each transport is its own opt-in package; the runtime never assumes a technology.

| Transport | Package | Status |
|---|---|---|
| In-memory | built into `Truss.Messaging` | Available |
| Postgres (LISTEN/NOTIFY) | `Truss.Messaging.Postgres` | Planned |
| Redis | `Truss.Messaging.Redis` | Planned |
| RabbitMQ | `Truss.Messaging.RabbitMq` | Planned |

The in-memory transport is intended for development, tests and modular monoliths: delivery happens in-process, after the commit, with no broker to run.

---

## Registration

```csharp
services.AddTrussMessaging(options =>
{
    options.AddAssembly<OrderPlaced>();
});

services.AddTrussInMemoryTransport();

services.AddTrussOutbox<AppDbContext>();
```

And add the outbox table to the context model:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyTrussOutbox();
}
```

`AddTrussMessaging` scans the registered assemblies for event types and handlers. `AddTrussOutbox` binds the store to the context, starts the processor and switches the publisher to transactional mode. Without it, a direct publisher sends straight to the transport, best effort.

---

## Configuration

Outbox behavior is configured through `TrussOutboxOptions`, either in code:

```csharp
services.AddTrussOutbox<AppDbContext>(options =>
{
    options.BatchSize = 100;
    options.MaxAttempts = 5;
});
```

or bound from configuration, which includes environment variables:

```csharp
services.Configure<TrussOutboxOptions>(configuration.GetSection("Truss:Outbox"));
```

```
Truss__Outbox__BatchSize=100
Truss__Outbox__PollingInterval=00:00:05
Truss__Outbox__MaxAttempts=5
```

Transport packages follow the same pattern: each exposes its own options type, configurable in code or from configuration and environment variables.

---

## Mapping Domain Events to Integration Events

There is no automatic translation, by design. When a domain occurrence must leave the service, declare it explicitly with a domain event handler that publishes the integration event:

```csharp
public class OrderPlacedTranslator(IIntegrationEventPublisher publisher) : IDomainEventHandler<OrderPlacedDomainEvent>
{
    public Task Handle(OrderPlacedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        return publisher.Publish(new OrderPlaced(domainEvent.OrderId.Value, domainEvent.Total), cancellationToken);
    }
}
```

Because domain event handlers run inside the transaction and the outbox stores in the same transaction, the translation is atomic with the command.
