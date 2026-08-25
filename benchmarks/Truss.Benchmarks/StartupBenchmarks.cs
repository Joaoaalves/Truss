using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Benchmarks.Reflection;

namespace Truss.Benchmarks
{
    /// <summary>
    /// Cold start: registering the application assembly, building the
    /// container and answering the first dispatch. This assembly carries the
    /// registrations Truss.Generators produced at compile time; the reflection
    /// contender lives in an assembly without the generator, so registration
    /// scans it at runtime, the path every scaffolded project has left behind.
    /// </summary>
    [MemoryDiagnoser]
    public class StartupBenchmarks
    {
        [Benchmark(Baseline = true)]
        public async Task<string> GeneratedRegistrations()
        {
            var services = new ServiceCollection();
            services.AddTruss(options => options.AddAssembly<TrussPing>());

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            return await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new TrussPing("hello"));
        }

        [Benchmark]
        public async Task<string> ReflectionScan()
        {
            var services = new ServiceCollection();
            services.AddTruss(options => options.AddAssembly<ReflectionPing>());

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            return await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new ReflectionPing("hello"));
        }
    }
}
