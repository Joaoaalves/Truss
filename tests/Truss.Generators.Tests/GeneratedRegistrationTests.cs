using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Generators.Tests.Fakes;
using Xunit;
using Truss.Application.Pipeline;
using Truss.Jobs.Runtime;
using Truss.Messaging.Dispatch;
using Truss.Messaging.Serialization;

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
        public void GeneratedModule_RegistersTheMessagingSliceAtStartup()
        {
            var assembly = typeof(GenItemCreated).Assembly;

            Assert.True(Truss.Messaging.Dispatch.TrussMessagingGeneratedRegistry.TryGetHandlers(assembly, out _));
            Assert.True(Truss.Messaging.Dispatch.TrussMessagingGeneratedRegistry.TryGetEventTypes(assembly, out var eventTypes));
            Assert.Contains(typeof(GenItemCreated), eventTypes);
        }

        [Fact]
        public void AddTrussMessaging_UsesTheGeneratedRegistration_ToResolveHandlers()
        {
            var services = new ServiceCollection();
            services.AddTrussMessaging(options => options.AddAssembly<GenItemCreated>());

            using var provider = services.BuildServiceProvider();

            Assert.IsType<GenItemCreatedHandler>(
                Assert.Single(provider.GetServices<Truss.Messaging.IIntegrationEventHandler<GenItemCreated>>()));

            var registry = provider.GetRequiredService<Truss.Messaging.Serialization.IntegrationEventTypeRegistry>();
            Assert.Equal("gen.item-created", registry.DescriptorFor(typeof(GenItemCreated)).Name);
        }

        [Fact]
        public void GeneratedModule_RegistersJobsWithTheirTypedInvokers()
        {
            Assert.True(Truss.Jobs.Runtime.TrussJobsGeneratedRegistry.TryGetJobs(typeof(GenReportJob).Assembly, out var jobs));

            var descriptor = Assert.Single(jobs, job => job.JobType == typeof(GenReportJob));
            Assert.Equal(typeof(GenReportArgs), descriptor.ArgsType);
            Assert.Equal(typeof(GenReportJob).FullName, descriptor.Name);
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
