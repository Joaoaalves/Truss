using Microsoft.Extensions.DependencyInjection;
using Truss.Application.Abstractions.Commands;
using Truss.Application.Abstractions.Dispatching;
using Truss.Application.Abstractions.Requests;
using Truss.Application.DependencyInjection;
using Truss.Application.Tests.Fakes;
using Xunit;

namespace Truss.Application.Tests.DispatchingTests
{
    public class DispatcherTests
    {
        private static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();

            services.AddTruss(options => options.AddAssembly<PingCommand>());
            configure?.Invoke(services);

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Send_Command_ReturnsHandlerResponse()
        {
            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            var response = await dispatcher.Send(new PingCommand("abc"));

            Assert.Equal("pong:abc", response);
        }

        [Fact]
        public async Task Send_VoidCommand_ReturnsUnit()
        {
            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            var response = await dispatcher.Send(new VoidCommand());

            Assert.Equal(Unit.Value, response);
        }

        [Fact]
        public async Task Send_Query_ReturnsHandlerResponse()
        {
            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            var response = await dispatcher.Send(new GetPingQuery());

            Assert.Equal("ping", response);
        }

        [Fact]
        public async Task Send_WithoutRegisteredHandler_Throws()
        {
            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.Send(new OrphanCommand())
            );

            Assert.Contains(nameof(OrphanCommand), exception.Message);
        }

        [Fact]
        public async Task Behaviors_ExecuteInRegistrationOrder()
        {
            var log = new CallLog();

            await using var provider = BuildProvider(services =>
            {
                services.AddSingleton(log);
                services.AddScoped<IPipelineBehavior<PingCommand, string>, OuterBehavior>();
                services.AddScoped<IPipelineBehavior<PingCommand, string>, InnerBehavior>();
            });

            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            await dispatcher.Send(new PingCommand("abc"));

            Assert.Equal(["outer:before", "inner:before", "inner:after", "outer:after"], log.Entries);
        }
    }
}
