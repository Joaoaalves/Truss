using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Persistence.EntityFrameworkCore.Tests.Fakes;
using Xunit;

namespace Truss.Persistence.EntityFrameworkCore.Tests
{
    public class CommandPipelineIntegrationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public CommandPipelineIntegrationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();

            services.AddSingleton<EventRecorder>();
            services.AddDbContext<TestDbContext>(options => options.UseSqlite(_connection));
            services.AddTruss(options => options.AddAssembly<PlaceOrderCommand>());
            services.AddTrussEntityFramework<TestDbContext>();

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
        }

        [Fact]
        public async Task Command_IsCommittedAutomatically_AndDomainEventsAreHandled()
        {
            var orderId = Guid.NewGuid();

            using (var scope = _provider.CreateScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
                await dispatcher.Send(new PlaceOrderCommand(orderId));
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var order = await context.Orders.SingleAsync(o => o.Id == orderId);
                Assert.Equal("Placed", order.Status);
            }

            var recorder = _provider.GetRequiredService<EventRecorder>();
            Assert.Contains(orderId, recorder.PlacedOrders);
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }
}
