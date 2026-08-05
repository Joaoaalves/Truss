using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Application.Tests.Fakes;
using Xunit;

namespace Truss.Application.Tests
{
    public class UnitOfWorkBehaviorTests
    {
        private static (ServiceProvider Provider, FakeUnitOfWork UnitOfWork) BuildProvider()
        {
            var unitOfWork = new FakeUnitOfWork();

            var services = new ServiceCollection();
            services.AddTruss(options => options.AddAssembly<PingCommand>());
            services.AddSingleton<IUnitOfWork>(unitOfWork);
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

            return (services.BuildServiceProvider(), unitOfWork);
        }

        [Fact]
        public async Task SuccessfulCommand_CommitsUnitOfWork()
        {
            var (provider, unitOfWork) = BuildProvider();
            await using var _ = provider;
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            await dispatcher.Send(new VoidCommand());

            Assert.Equal(1, unitOfWork.Commits);
        }

        [Fact]
        public async Task Query_DoesNotCommitUnitOfWork()
        {
            var (provider, unitOfWork) = BuildProvider();
            await using var _ = provider;
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            await dispatcher.Send(new GetPingQuery());

            Assert.Equal(0, unitOfWork.Commits);
        }

        [Fact]
        public async Task FailingCommand_DoesNotCommitUnitOfWork()
        {
            var (provider, unitOfWork) = BuildProvider();
            await using var _ = provider;
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.Send(new ThrowingCommand())
            );

            Assert.Equal(0, unitOfWork.Commits);
        }
    }
}
