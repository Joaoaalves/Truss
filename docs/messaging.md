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

Delivery starts immediately: every commit that stores outbox messages wakes the processor in-process, so events reach the transport right after the transaction, not on the next poll. Polling (15 seconds by default) remains only as the safety net for retries and for messages written by other application instances.

Running more than one instance is fine. On PostgreSQL and SQL Server each fetch claims its batch with SKIP LOCKED semantics, so instances that wake up together pick disjoint batches instead of publishing the same message twice. Providers without the feature fall back to the plain query, where a rare overlap between instances only means a duplicate publish.

Delivery is **at-least-once** either way: a crash between publishing and marking a message processed still replays it, so consumers must be idempotent.

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
| Postgres | `Truss.Messaging.Postgres` | Available |
| RabbitMQ | `Truss.Messaging.RabbitMq` | Available |
| Redis | `Truss.Messaging.Redis` | Available |

The in-memory transport is intended for development, tests and modular monoliths: delivery happens in-process, after the commit, with no broker to run. Delivery is still asynchronous; `InMemoryTransport.WaitForIdle` completes once every published message has been handled, which is what the [test host](testing.md) uses to make delivery deterministic.

### Postgres

The Postgres transport turns a table into a durable queue. Publishing inserts the envelope and raises a NOTIFY; consumers use LISTEN as a low-latency wake-up signal, but delivery never depends on the notification: rows are claimed with `FOR UPDATE SKIP LOCKED`, so multiple application instances compete safely and a message published while every consumer was offline is delivered when one comes back. Failed messages retry with exponential backoff and are moved to a dead-letter table with the error preserved. The transport creates its own tables by default.

```csharp
services.AddTrussPostgresTransport(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Messaging")!;
});
```

If the application already uses Postgres, this is messaging with zero extra infrastructure.

### Redis

The Redis transport uses Streams with consumer groups. Publishing appends to a stream; each application instance consumes through the group, so delivery survives restarts and messages are balanced between instances. A message left pending by a failed handler is reclaimed after an idle period and retried; once the delivery limit is reached it moves to a dead-letter stream. The main stream is trimmed approximately to a configurable length.

```csharp
services.AddTrussRedisTransport(options =>
{
    options.ConnectionString = "localhost:6379";
});
```

### RabbitMQ

The RabbitMQ transport publishes to a durable quorum queue with publisher confirms: a completed publish is on disk at the broker. Retry accounting lives at the broker through the quorum queue delivery limit, so it holds across restarts and competing consumers; a message that exhausts its limit is dead-lettered by the broker itself into a `.dead` queue. A failed handler briefly pauses the consumer before returning the message, throttling hot redelivery loops.

```csharp
services.AddTrussRabbitMqTransport(options =>
{
    options.ConnectionString = "amqp://guest:guest@localhost:5672";
});
```

Multiple application instances consuming the same queue compete for messages, which is the natural scale-out path.

### Publisher-only applications

Every broker transport hosts a consumer by default. Set `EnableConsumer = false` on services that only publish.

### Scaling consumption out

`truss add worker` scaffolds a separate consumer process sharing the application and infrastructure layers. With a durable transport, the worker and the API compete for messages and jobs, so consumption scales by running more workers; the scheduler locks keep scheduled and recurring jobs firing exactly once across all of them. A common production shape is the API with `EnableConsumer = false` and any number of workers doing the consuming.

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
Truss__Outbox__RetentionPeriod=7.00:00:00
```

Transport packages follow the same pattern: each exposes its own options type, configurable in code or from configuration and environment variables.

### Retention

Processed messages are deleted after `RetentionPeriod`, 3 days by default, swept at most once per `CleanupInterval` (1 hour). Set `RetentionPeriod` to null to keep every processed message. Dead-lettered messages are never deleted; they stay for inspection and reprocessing.

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
