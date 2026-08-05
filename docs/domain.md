# Building Blocks

Everything in `Truss.Domain` lives in a single namespace. One using gives you the whole domain kernel:

```csharp
using Truss.Domain;
```

---

## Entities

Entities are objects with identity. Two entities are equal when they have the same type and the same identifier, never by their attributes.

```csharp
public class Product : Entity<ProductId>
{
    private Product() { }                    // ORM materialization only

    public Product(ProductId id, string name) : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; }
}
```

Equality rules:

- Same type and same id: equal.
- Different types: never equal, even with the same id.
- Transient entities (no id assigned yet): never equal to anything but themselves.

---

## Aggregate Roots

An aggregate root is the entry point to a cluster of domain objects and its consistency boundary. Only aggregate roots are loaded and persisted directly.

```csharp
public class Order : AggregateRoot<OrderId>
{
    public void Place()
    {
        CheckRule(new OrderMustHaveItemsRule(_items));

        Status = OrderStatus.Placed;
        AddDomainEvent(new OrderPlaced(Id));
    }
}
```

`AggregateRoot<TId>` extends `Entity<TId>` and implements the `IAggregateRoot` marker.

---

## Value Objects

Value objects are immutable and compared by their equality components. The components are declared explicitly, with no reflection involved:

```csharp
public class Money(decimal amount, string currency) : ValueObject
{
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
```

Two value objects are equal when their components are equal, in the same order.

---

## Typed Identifiers

`TypedId<TValue>` gives every aggregate its own identifier type, so a `UserId` can never be passed where an `OrderId` is expected:

```csharp
public sealed record OrderId(Guid Value) : TypedId<Guid>(Value);
public sealed record SkuCode(string Value) : TypedId<string>(Value);
```

Typed ids are records: value equality and deconstruction come for free, and `ToString()` returns the underlying value.

---

## Business Rules

Business rules encapsulate domain invariants as first-class objects:

```csharp
public class OrderMustHaveItemsRule(IReadOnlyCollection<OrderItem> items) : IBusinessRule
{
    public bool IsBroken() => items.Count == 0;

    public string Message => "An order must contain at least one item.";
}
```

Enforce them with `CheckRule` inside entities and value objects:

```csharp
CheckRule(new OrderMustHaveItemsRule(_items));
```

When a rule is broken, a `BusinessRuleValidationException` is thrown carrying the rule instance and its message.

> Business rules protect domain invariants. Input validation belongs to the [validation pipeline](pipeline.md). By the time a command reaches the domain, its shape is already valid.

---

## Domain Events

Domain events represent something meaningful that has happened in the domain. Raise them inside aggregates:

```csharp
public sealed record OrderPlaced(OrderId OrderId) : DomainEvent;
```

```csharp
AddDomainEvent(new OrderPlaced(Id));
```

The `DomainEvent` base record captures `OccurredOn` at creation time. Events accumulate on the entity and are collected and dispatched by the [unit of work](unit-of-work.md). The domain layer never dispatches anything itself.
