# Source Generators

`Truss.Generators` moves handler discovery from startup to build time. It is a Roslyn source generator and analyzer packaged as a development dependency: it changes what gets compiled, never what gets deployed.

---

## What It Does

Installed in the composition root project, the generator scans the compilation and every referenced assembly that uses Truss at build time, and generates a registration module for the handlers, validators and domain event handlers it finds.

With the generator present:

- `AddTruss` uses the generated registrations. No assembly scanning happens at startup.
- Dispatch invokers are prepared at build time. The first dispatch of each request type no longer pays a reflection cost.
- Wrong wiring surfaces as build diagnostics instead of runtime surprises.

Without the generator, everything keeps working exactly as before: `AddTruss` falls back to runtime scanning per assembly. The generator is an accelerator, not a requirement.

---

## Installation

Add the package to the project that calls `AddTruss` (the API or host project):

```xml
<PackageReference Include="Truss.Generators" Version="x.y.z" PrivateAssets="all" />
```

`PrivateAssets="all"` keeps it out of your published dependencies. There is no configuration and no code to write: the registration API does not change.

```csharp
services.AddTruss(options =>
{
    options.AddAssembly<CreateUser>();
});
```

At build time the generator emits a module that registers each discovered assembly. At startup, `AddTruss` finds the generated registration for the assembly and uses it.

---

## Build Diagnostics

| Id | Severity | Meaning |
|---|---|---|
| TRUSS001 | Warning | A command or query has no handler anywhere in the compilation |
| TRUSS002 | Error | A command or query has more than one handler |
| TRUSS003 | Info | An assembly has Truss implementations not accessible to generated code; runtime scanning is used for that assembly |

TRUSS002 is an error by design: with two handlers registered, dispatch would silently use one of them. That is a bug worth failing the build for.

---

## Scope and Rules

- The generator scans the current compilation plus referenced assemblies that reference `Truss.Application.Abstractions`. Framework and Truss package assemblies are never scanned.
- Handlers and validators must be accessible from the composition root (public, or internal with `InternalsVisibleTo`). When they are not, the whole assembly falls back to runtime scanning and TRUSS003 is reported.
- Generic handler types are ignored; register open generics manually as pipeline behaviors.

---

## Ahead-of-Time Compilation

The generated path uses no `dynamic`, no `MakeGenericType` and no `Activator`. Combined with the dispatcher's typed invokers, applications using the generator can target Native AOT. The runtime scanning fallback is the only reflection-dependent path, and it never runs when a generated registration exists for the assembly.
