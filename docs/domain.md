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

### Invariants live in the value object

A primitive has nowhere to keep a rule, which is how validation drifts into handlers and the domain goes anemic. The strongest pattern Truss encourages is the self-validating value object: a private constructor, a `Create` factory that normalizes and checks rules, and therefore a type whose instances are valid by construction. `ValueObject` carries the same `CheckRule` as aggregates, so a broken invariant surfaces as the same 422 with a stable code:

```csharp
public sealed class FoodName : ValueObject
{
    public const int MaxLength = 200;

    private FoodName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static FoodName Create(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;

        CheckRule(new FoodNameMustNotBeEmpty(normalized));
        CheckRule(new FoodNameMustFitLength(normalized));

        return new FoodName(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

An aggregate whose `Name` is a `FoodName` cannot hold an invalid name; the rule has one home, and the state that would break it is unrepresentable. Value objects have no identity and raise no events, and the boundary stays primitive: commands carry strings and numbers, handlers convert through `Create`, and validators keep checking shape for the 400 while the value object guards the invariant for the 422.

The [CLI generates this shape](cli.md#truss-generate): `truss g agg Food -c Nutrition --vo Name:string --vo Calories:int` builds each value object in its own folder with its rules and tests, and `truss g vo Money -c Shared -f Amount:decimal -f Currency:string` builds shared, multi-field ones.

### Mapping value objects with EF Core

A single-value wrapper maps as a conversion, which is exactly what the generator writes into the aggregate's configuration:

```csharp
builder.Property(food => food.Name)
    .HasConversion(name => name.Value, value => FoodName.Create(value))
    .HasMaxLength(FoodName.MaxLength)
    .IsRequired();
```

Rehydration goes through `Create` on purpose: a row that no longer satisfies the invariant fails loudly instead of resurrecting an invalid object.

A value object with several members must not use a conversion. Map it as a complex type, which stores each member as a column of the owner's table while keeping the type without identity:

```csharp
builder.ComplexProperty(order => order.Price, price =>
{
    price.Property(money => money.Amount)
        .HasColumnName("PriceAmount")
        .HasPrecision(18, 2);

    price.Property(money => money.Currency)
        .HasColumnName("PriceCurrency")
        .HasMaxLength(Money.CurrencyMaxLength);
});
```

Complex types are the correct mapping for multi-member value objects on EF Core 8 and later: no separate table, no shadow key, and the object stays immutable. Reach for owned entities (`OwnsOne`/`OwnsMany`) only when you need a collection of value objects or a separate table; they carry a hidden key, which a value object conceptually does not have.

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

Every rule also has a `Code`, the stable machine-readable identifier API clients receive in 422 responses. It defaults to the type name; override it when clients depend on the value and the type might be renamed:

```csharp
public string Code => "orders.no-items";
```

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
