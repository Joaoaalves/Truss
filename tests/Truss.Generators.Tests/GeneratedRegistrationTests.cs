using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Generators.Tests.Fakes;
using Xunit;

namespace Truss.Generators.Tests
{
    public class GeneratedRegistrationTests
    {
        [Fact]
        public void GeneratedModule_RegistersThisAssemblyAtStartup()
        {
            Assert.True(TrussGeneratedRegistry.HasRegistrationFor(typeof(GenPingCommand).Assembly));
        }

        [Fact]
        public async Task AddTruss_UsesGeneratedRegistration_ToDispatchCommands()
        {
            var services = new ServiceCollection();
            services.AddTruss(options => options.AddAssembly<GenPingCommand>());

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            var response = await dispatcher.Send(new GenPingCommand("abc"));

            Assert.Equal("gen:abc", response);
        }

        [Fact]
        public async Task AddTruss_UsesGeneratedRegistration_ToRunValidators()
        {
            var services = new ServiceCollection();
            services.AddTruss(options => options.AddAssembly<GenPingCommand>());

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            await Assert.ThrowsAsync<RequestValidationException>(
                () => dispatcher.Send(new GenPingCommand(""))
            );
        }

        [Fact]
        public async Task AddTruss_UsesGeneratedRegistration_ToDispatchDomainEvents()
        {
            var recorder = new GenEventRecorder();
            var services = new ServiceCollection();
            services.AddSingleton(recorder);
            services.AddTruss(options => options.AddAssembly<GenPingCommand>());

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

            var domainEvent = new GenEvent(Guid.NewGuid());
            await dispatcher.Dispatch([domainEvent]);

            Assert.Contains(domainEvent.Id, recorder.Handled);
        }
    }
}
