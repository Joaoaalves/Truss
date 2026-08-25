using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Messaging.Tests.Fakes;
using Xunit;
using Truss.Messaging.Dispatch;
using Truss.Messaging.Serialization;

namespace Truss.Messaging.Tests
{
    public class DispatchTests
    {
        private static (ServiceProvider Provider, ReceivedEvents Received, FakeUnitOfWork UnitOfWork) BuildProvider()
        {
            var received = new ReceivedEvents();
            var unitOfWork = new FakeUnitOfWork();

            var services = new ServiceCollection();
            services.AddSingleton(received);
            services.AddSingleton<IUnitOfWork>(unitOfWork);
            services.AddTrussMessaging(options => options.AddAssembly<ItemCreated>());

            return (services.BuildServiceProvider(), received, unitOfWork);
        }

        [Fact]
        public async Task Dispatch_InvokesHandlers_AndCommitsUnitOfWork()
        {
            var (provider, received, unitOfWork) = BuildProvider();
            await using var _ = provider;

            var serializer = provider.GetRequiredService<IIntegrationEventSerializer>();
            var dispatcher = provider.GetRequiredService<IIntegrationEventDispatcher>();
            var integrationEvent = new ItemCreated(Guid.NewGuid());

            await dispatcher.Dispatch(serializer.Serialize(integrationEvent));

            var handled = Assert.Single(received.Snapshot());
            Assert.Equal(integrationEvent.Id, handled.Id);
            Assert.Equal(1, unitOfWork.Commits);
        }

        [Fact]
        public async Task Dispatch_HandlerFailure_Propagates_AndDoesNotCommit()
        {
            var (provider, _, unitOfWork) = BuildProvider();
            await using var __ = provider;

            var serializer = provider.GetRequiredService<IIntegrationEventSerializer>();
            var dispatcher = provider.GetRequiredService<IIntegrationEventDispatcher>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.Dispatch(serializer.Serialize(new ThrowingEvent()))
            );

            Assert.Equal(0, unitOfWork.Commits);
        }
    }
}
