# Benchmarks

Measured with BenchmarkDotNet on .NET 10, comparing Truss's dispatcher with MediatR 12, the most common alternative. The benchmark project lives in `benchmarks/` in the repository; reproduce everything with:

```
dotnet run -c Release --project benchmarks/Truss.Benchmarks -- --filter "*"
```

The comparison is deliberately level: Truss always ships its FluentValidation behavior in the pipeline, so MediatR runs with the identical validation behavior registered, written the same way. Nobody runs MediatR bare in a real application either.

---

## Dispatching a command

One command through the whole pipeline, handler resolved from the scope, steady state:

| Scenario | Truss | MediatR |
|---|---|---|
| Command through the pipeline | **147 ns, 328 B** | 159 ns, 464 B |
| Command with an active validator | **686 ns, 1.7 KB** | 1962 ns, 4.1 KB |

Same work, same validation passenger: Truss dispatches with about 30 percent less allocation, and validated commands run close to three times faster. Both are, to be clear, noise next to a single database call; the number that matters is that the abstraction costs nanoseconds, not that it wins them.

---

## Cold start

Registering the application assembly, building the container and answering the first dispatch:

| Path | Time | Allocated |
|---|---|---|
| Compile-time registrations (Truss.Generators) | **6.6 us** | 12.1 KB |
| Runtime reflection scan | 7.4 us | 12.3 KB |

The benchmark assembly holds a handful of handlers, so the absolute gap is small here and grows with every type the scan would have to walk. The generator's real value is not the microseconds: it is `TRUSS001` failing the build when a request has no handler, duplicate handlers failing it too, and no reflection left between your code and Native AOT.

---

## Method

- BenchmarkDotNet, Release, one process per benchmark, memory diagnoser on.
- Handlers do no work, so the numbers isolate dispatch overhead and nothing else.
- MediatR runs with the same FluentValidation open behavior Truss ships, registered the standard way.
- Numbers from an AMD Ryzen 7 5700G under WSL2; run the project on your machine for yours.
