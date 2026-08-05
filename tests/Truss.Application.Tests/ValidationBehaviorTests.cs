using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Application.Tests.Fakes;
using Xunit;

namespace Truss.Application.Tests
{
    public class ValidationBehaviorTests
    {
        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddTruss(options => options.AddAssembly<PingCommand>());
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task InvalidRequest_ThrowsWithAllFailures()
        {
            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            var exception = await Assert.ThrowsAsync<RequestValidationException>(
                () => dispatcher.Send(new PingCommand(""))
            );

            Assert.Equal(2, exception.Errors.Count);
            Assert.All(exception.Errors, error => Assert.Equal(nameof(PingCommand.Value), error.PropertyName));
        }

        [Fact]
        public async Task ValidRequest_ReachesHandler()
        {
            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            var response = await dispatcher.Send(new PingCommand("abc"));

            Assert.Equal("pong:abc", response);
        }
    }
}
