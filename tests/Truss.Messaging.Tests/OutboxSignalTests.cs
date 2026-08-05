using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truss.Application;
using Truss.Messaging.Tests.Fakes;
using Xunit;

namespace Truss.Messaging.Tests
{
    public class OutboxSignalTests : IAsyncLifetime
    {
        private readonly string _databasePath;
        private readonly ServiceProvider _provider;

        public OutboxSignalTests()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"truss-messaging-{Guid.NewGuid():N}.db");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ReceivedEvents>();
            services.AddDbContext<MessagingDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
            services.AddTruss(options => options.AddAssembly<CreateItemCommand>());
            services.AddTrussEntityFramework<MessagingDbContext>();
            services.AddTrussMessaging(options => options.AddAssembly<CreateItemCommand>());
            services.AddTrussInMemoryTransport();
            services.AddTrussOutbox<MessagingDbContext>(options =>
            {
                options.PollingInterval = TimeSpan.FromSeconds(30);
            });

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.EnsureCreated();
        }

        public async Task InitializeAsync()
        {
            foreach (var hostedService in _provider.GetServices<IHostedService>())
                await hostedService.StartAsync(CancellationToken.None);
        }

        [Fact]
        public async Task CommittedEvent_IsDelivered_LongBeforeThePollingInterval()
        {
            var itemId = Guid.NewGuid();

            using (var scope = _provider.CreateScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
                await dispatcher.Send(new CreateItemCommand(itemId));
            }

            var received = _provider.GetRequiredService<ReceivedEvents>();
            var deadline = DateTime.UtcNow.AddSeconds(8);

            while (DateTime.UtcNow < deadline && received.Snapshot().Count == 0)
                await Task.Delay(25);

            var handled = Assert.Single(received.Snapshot());
            Assert.Equal(itemId, Assert.IsType<ItemCreated>(handled).ItemId);
        }

        public async Task DisposeAsync()
        {
            foreach (var hostedService in _provider.GetServices<IHostedService>())
                await hostedService.StopAsync(CancellationToken.None);

            await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }
    }
}
