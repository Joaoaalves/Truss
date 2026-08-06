using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Jobs;
using Truss.Testing.Tests.Fakes;
using Xunit;

namespace Truss.Testing.Tests
{
    public class TrussTestHostTests
    {
        private static Task<TrussTestHost> StartOrdersHost()
        {
            return TrussTestHost.Start<OrdersDbContext>(options =>
            {
                options.AddAssembly<PlaceOrder>();
                options.UseJobs();
                options.ConfigureServices(services => services.AddSingleton<ReceivedEvents>());
            });
        }

        [Fact]
        public async Task Send_RunsTheFullPipeline_AndCommits()
        {
            await using var host = await StartOrdersHost();

            var orderId = await host.Send(new PlaceOrder("Beam"));

            var stored = await host.ExecuteScoped(provider =>
                provider.GetRequiredService<OrdersDbContext>().Orders.SingleAsync(order => order.Id == orderId));

            Assert.Equal("Beam", stored.Name);
        }

        [Fact]
        public async Task Send_InvalidCommand_ThrowsWithTheValidationFailures()
        {
            await using var host = await StartOrdersHost();

            var exception = await Assert.ThrowsAsync<RequestValidationException>(
                () => host.Send(new PlaceOrder("")));

            Assert.Contains(exception.Errors, error => error.PropertyName == "Name");
        }

        [Fact]
        public async Task DrainOutbox_DeliversTheEventToItsHandler()
        {
            await using var host = await StartOrdersHost();

            var orderId = await host.Send(new PlaceOrder("Beam"));
            await host.DrainOutbox();

            var received = host.Services.GetRequiredService<ReceivedEvents>();
            Assert.Contains(received.Events, integrationEvent => integrationEvent.OrderId == orderId);
        }

        [Fact]
        public async Task WaitForJob_FollowsTheJobToCompletion()
        {
            await using var host = await StartOrdersHost();

            var jobId = await host.Send(new StartExport("catalog"));

            var snapshot = await host.WaitForJob(jobId, JobStatus.Succeeded);
            Assert.Equal(100, snapshot.ProgressPercent);
            Assert.Equal("Done", snapshot.ProgressMessage);
        }

        [Fact]
        public async Task HostWithoutDatabase_DispatchesRequests()
        {
            await using var host = await TrussTestHost.Start(options => options.AddAssembly<Ping>());

            Assert.Equal("pong: hi", await host.Send(new Ping("hi")));
        }

        [Fact]
        public async Task DrainOutbox_WithoutOutbox_ExplainsWhatIsMissing()
        {
            await using var host = await TrussTestHost.Start(options => options.AddAssembly<Ping>());

            await Assert.ThrowsAsync<InvalidOperationException>(() => host.DrainOutbox());
        }
    }
}
