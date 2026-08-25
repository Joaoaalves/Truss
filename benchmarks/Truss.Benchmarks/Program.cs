using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Truss.Benchmarks.DispatchBenchmarks).Assembly).Run(args);
