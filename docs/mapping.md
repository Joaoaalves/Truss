# DTO Mapping

Truss implements your mappers at compile time. You declare the signature; the generator writes the body. There is no runtime library, no reflection and no configuration API: what cannot be mapped fails the build.

The mapping generator ships inside `Truss.Generators`, which every scaffolded project already references. There is nothing to install: declare a mapper and the build fills it in.

```xml
<PackageReference Include="Truss.Generators" Version="x.y.z" PrivateAssets="all" />
```

---

## Declaring Mappers

A mapper is a static partial class with partial methods from source to target:

```csharp
[Mapper]
public static partial class CatalogMappings
{
    public static partial ProductDto ToDto(Product product);

    public static partial OrderLineDto ToDto(OrderLine line);
}
```

The generator implements each method:

- Members map by name, case-insensitive.
- The target's richest satisfiable constructor is used, which makes positional records the natural DTO shape; remaining settable properties are covered by an object initializer.
- A typed id unwraps automatically: a `ProductId` source member maps to a `Guid` target member through its `Value`. This is the mapper understanding the Truss domain idiom.
- Nested types map through the other methods of the same class: `Product.Lines` of `List<OrderLine>` maps to `List<OrderLineDto>` by calling `ToDto` per element.
- Collections translate between arrays, `List<T>` and the common read-only interfaces.

```csharp
public sealed record ProductDto(Guid Id, string Name, decimal Price, IReadOnlyList<OrderLineDto> Lines);

var dto = CatalogMappings.ToDto(product);
```

---

## Computed Members

When the target needs something the source does not expose as a property, write a regular static method named after the member:

```csharp
public sealed record ProductSummaryDto(Guid Id, string Name)
{
    public int LineCount { get; init; }
}

[Mapper]
public static partial class CatalogMappings
{
    public static partial ProductSummaryDto ToSummary(Product product);

    public static int LineCount(Product product) => product.Lines.Count;
}
```

The generator finds `LineCount` by name and uses it. The same principle replaces any generated mapping: implement the method yourself instead of declaring it partial, and every nested mapping that needs that type pair uses your version.

---

## Build Diagnostics

| Id | Severity | Meaning |
|---|---|---|
| TRUSSMAP001 | Error | A target member has no matching source member or supported conversion |
| TRUSSMAP002 | Error | The mapper class or a mapping method is declared incorrectly |

Failing the build is the point: a DTO gains a field, and every mapper that does not account for it stops compiling, instead of shipping a default value to production.

---

## Scope

The mapping generator deliberately covers the framework's idiom: aggregates and value objects projected into DTO records. It does not do flattening of nested paths, enum renaming or bidirectional configuration. If you need a fully featured mapper, [Mapperly](https://github.com/riok/mapperly) is excellent, Apache-2.0 licensed and also compile-time; both can coexist in the same solution.
