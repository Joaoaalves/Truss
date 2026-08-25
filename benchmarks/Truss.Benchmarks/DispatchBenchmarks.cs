using BenchmarkDotNet.Attributes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application;

namespace Truss.Benchmarks
{
    /// <summary>
    /// Steady-state dispatch: one command through the whole pipeline, handler
    /// resolved from the scope, on every contender. The validated variant runs
    /// the FluentValidation behavior too, which the bare ones do not have.
    /// </summary>
    [MemoryDiagnoser]
    public class DispatchBenchmarks
    {
        private ServiceProvider _trussProvider = null!;
        private IServiceScope _trussScope = null!;
        private IDispatcher _dispatcher = null!;

        private ServiceProvider _mediatorProvider = null!;
        private IServiceScope _mediatorScope = null!;
        private IMediator _mediator = null!;

        [GlobalSetup]
        public void Setup()
        {
            var truss = new ServiceCollection();
            truss.AddTruss(options => options.AddAssembly<TrussPing>());
            _trussProvider = truss.BuildServiceProvider();
            _trussScope = _trussProvider.CreateScope();
            _dispatcher = _trussScope.ServiceProvider.GetRequiredService<IDispatcher>();

            var mediator = new ServiceCollection();

            mediator.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(typeof(MediatorPing).Assembly);
                configuration.AddOpenBehavior(typeof(MediatorValidationBehavior<,>));
            });

            mediator.AddTransient<FluentValidation.IValidator<MediatorValidatedPing>, MediatorValidatedPingValidator>();
            _mediatorProvider = mediator.BuildServiceProvider();
            _mediatorScope = _mediatorProvider.CreateScope();
            _mediator = _mediatorScope.ServiceProvider.GetRequiredService<IMediator>();
        }

        [Benchmark(Baseline = true)]
        public Task<string> Truss_Command()
        {
            return _dispatcher.Send(new TrussPing("hello"));
        }

        [Benchmark]
        public Task<string> MediatR_Command()
        {
            return _mediator.Send(new MediatorPing("hello"));
        }

        [Benchmark]
        public Task<string> Truss_Command_WithValidator()
        {
            return _dispatcher.Send(new TrussValidatedPing("hello"));
        }

        [Benchmark]
        public Task<string> MediatR_Command_WithValidator()
        {
            return _mediator.Send(new MediatorValidatedPing("hello"));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _trussScope.Dispose();
            _trussProvider.Dispose();
            _mediatorScope.Dispose();
            _mediatorProvider.Dispose();
        }
    }
}
