using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truss.Application;
using Truss.Messaging.Tests.Fakes;
using Xunit;
using Truss.Messaging.Outbox;

namespace Truss.Messaging.Tests
{
    public class EndToEndTests : IAsyncLifetime
    {
        private readonly string _databasePath;
        private readonly ServiceProvider _provider;

        public EndToEndTests()
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
                options.PollingInterval = TimeSpan.FromMilliseconds(50);
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
        public async Task Command_PublishesThroughOutbox_AndHandlerReceivesEvent()
        {
            var itemId = Guid.NewGuid();

            using (var scope = _provider.CreateScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
                await dispatcher.Send(new CreateItemCommand(itemId));
            }

            var received = _provider.GetRequiredService<ReceivedEvents>();
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < deadline && received.Snapshot().Count == 0)
                await Task.Delay(25);

            var handled = Assert.Single(received.Snapshot());
            var itemCreated = Assert.IsType<ItemCreated>(handled);
            Assert.Equal(itemId, itemCreated.ItemId);

            // The handler can observe the event before the processor persists the
            // processed status; wait for the status instead of asserting instantly.
            var status = OutboxMessageStatus.Pending;
            var statusDeadline = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < statusDeadline)
            {
                using var verification = _provider.CreateScope();
                var context = verification.ServiceProvider.GetRequiredService<MessagingDbContext>();
                status = (await context.Set<OutboxMessage>().SingleAsync()).Status;

                if (status == OutboxMessageStatus.Processed)
                    break;

                await Task.Delay(25);
            }

            Assert.Equal(OutboxMessageStatus.Processed, status);
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
