using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truss.Application;
using Truss.Messaging.Tests.Fakes;
using Xunit;

namespace Truss.Messaging.Tests
{
    public class EndToEndTests : IAsyncLifetime
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public EndToEndTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ReceivedEvents>();
            services.AddDbContext<MessagingDbContext>(options => options.UseSqlite(_connection));
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

            using var verification = _provider.CreateScope();
            var context = verification.ServiceProvider.GetRequiredService<MessagingDbContext>();
            var message = await context.Set<OutboxMessage>().SingleAsync();
            Assert.Equal(OutboxMessageStatus.Processed, message.Status);
        }

        public async Task DisposeAsync()
        {
            foreach (var hostedService in _provider.GetServices<IHostedService>())
                await hostedService.StopAsync(CancellationToken.None);

            await _provider.DisposeAsync();
            _connection.Dispose();
        }
    }
}
