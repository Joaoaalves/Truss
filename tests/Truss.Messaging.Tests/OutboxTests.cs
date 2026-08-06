using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.Messaging.Tests.Fakes;
using Xunit;

namespace Truss.Messaging.Tests
{
    public class OutboxTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly FakeTransport _transport;

        public OutboxTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _transport = new FakeTransport();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ReceivedEvents>();
            services.AddDbContext<MessagingDbContext>(options => options.UseSqlite(_connection));
            services.AddTruss(options => options.AddAssembly<CreateItemCommand>());
            services.AddTrussEntityFramework<MessagingDbContext>();
            services.AddTrussMessaging(options => options.AddAssembly<CreateItemCommand>());
            services.AddSingleton<IMessageTransport>(_transport);
            services.AddTrussOutbox<MessagingDbContext>(options =>
            {
                options.RetryBaseDelay = TimeSpan.FromMilliseconds(1);
                options.MaxAttempts = 2;
            });

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.EnsureCreated();
        }

        private Task SendAsync(Truss.Application.ICommand command)
        {
            return SendAsync(_provider, command);
        }

        private static async Task SendAsync(ServiceProvider provider, Truss.Application.ICommand command)
        {
            using var scope = provider.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.Send(command);
        }

        private OutboxMessage SingleStoredMessage()
        {
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            return context.Set<OutboxMessage>().Single();
        }

        [Fact]
        public async Task PublishInsideCommand_StoresMessageAtomically_WithoutTouchingTransport()
        {
            var itemId = Guid.NewGuid();

            await SendAsync(new CreateItemCommand(itemId));

            Assert.Empty(_transport.Published);
            var message = SingleStoredMessage();
            Assert.Equal("test.item-created", message.Name);
            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.Contains(itemId.ToString(), message.Payload);
        }

        [Fact]
        public async Task FailingCommand_StoresNothing()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => SendAsync(new FailingCommand())
            );

            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            Assert.Equal(0, await context.Set<OutboxMessage>().CountAsync());
        }

        [Fact]
        public async Task Processor_PublishesDueMessages_AndMarksThemProcessed()
        {
            await SendAsync(new CreateItemCommand(Guid.NewGuid()));

            var processor = _provider.GetRequiredService<OutboxProcessor>();
            var processed = await processor.ProcessPendingAsync();

            Assert.Equal(1, processed);
            var envelope = Assert.Single(_transport.Published);
            Assert.Equal("test.item-created", envelope.Name);

            var message = SingleStoredMessage();
            Assert.Equal(OutboxMessageStatus.Processed, message.Status);
            Assert.NotNull(message.ProcessedOn);
        }

        [Fact]
        public async Task Processor_SchedulesRetry_WhenTransportFails()
        {
            await SendAsync(new CreateItemCommand(Guid.NewGuid()));
            _transport.Fail = true;

            var processor = _provider.GetRequiredService<OutboxProcessor>();
            await processor.ProcessPendingAsync();

            var message = SingleStoredMessage();
            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.Equal(1, message.Attempts);
            Assert.NotNull(message.NextAttemptOn);
            Assert.NotNull(message.Error);

            _transport.Fail = false;
            await Task.Delay(20);
            await processor.ProcessPendingAsync();

            Assert.Single(_transport.Published);
            Assert.Equal(OutboxMessageStatus.Processed, SingleStoredMessage().Status);
        }

        [Fact]
        public async Task Processor_CleansProcessedMessages_PastRetention_ButKeepsFailed()
        {
            var transport = new FakeTransport();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ReceivedEvents>();
            services.AddDbContext<MessagingDbContext>(options => options.UseSqlite(_connection));
            services.AddTruss(options => options.AddAssembly<CreateItemCommand>());
            services.AddTrussEntityFramework<MessagingDbContext>();
            services.AddTrussMessaging(options => options.AddAssembly<CreateItemCommand>());
            services.AddSingleton<IMessageTransport>(transport);
            services.AddTrussOutbox<MessagingDbContext>(options =>
            {
                options.RetryBaseDelay = TimeSpan.FromMilliseconds(1);
                options.MaxAttempts = 1;
                options.RetentionPeriod = TimeSpan.FromMilliseconds(1);
                options.CleanupInterval = TimeSpan.Zero;
            });

            var provider = services.BuildServiceProvider();
            await using var _ = provider;

            transport.Fail = true;
            await SendAsync(provider, new CreateItemCommand(Guid.NewGuid()));
            var processor = provider.GetRequiredService<OutboxProcessor>();
            await processor.ProcessPendingAsync();

            transport.Fail = false;
            await SendAsync(provider, new CreateItemCommand(Guid.NewGuid()));
            await processor.ProcessPendingAsync();

            await Task.Delay(20);
            await processor.ProcessPendingAsync();

            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            var remaining = await context.Set<OutboxMessage>().ToListAsync();
            var message = Assert.Single(remaining);
            Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        }

        [Fact]
        public async Task Statistics_AndHealthCheck_ReflectTheOutboxState()
        {
            await SendAsync(new CreateItemCommand(Guid.NewGuid()));

            using (var scope = _provider.CreateScope())
            {
                var statistics = await scope.ServiceProvider.GetRequiredService<IOutboxStore>().GetStatistics();
                Assert.Equal(1, statistics.PendingCount);
                Assert.Equal(0, statistics.FailedCount);
                Assert.NotNull(statistics.OldestPendingOccurredOn);
            }

            _transport.Fail = true;
            var processor = _provider.GetRequiredService<OutboxProcessor>();
            await processor.ProcessPendingAsync();
            await Task.Delay(20);
            await processor.ProcessPendingAsync();

            using (var scope = _provider.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

                var statistics = await store.GetStatistics();
                Assert.Equal(0, statistics.PendingCount);
                Assert.Equal(1, statistics.FailedCount);

                var check = new OutboxHealthCheck(store, TimeProvider.System, new TrussOutboxHealthOptions());
                var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

                Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, result.Status);
            }
        }

        [Fact]
        public async Task Processor_DeadLettersMessage_AfterMaxAttempts()
        {
            await SendAsync(new CreateItemCommand(Guid.NewGuid()));
            _transport.Fail = true;

            var processor = _provider.GetRequiredService<OutboxProcessor>();
            await processor.ProcessPendingAsync();
            await Task.Delay(20);
            await processor.ProcessPendingAsync();

            var message = SingleStoredMessage();
            Assert.Equal(OutboxMessageStatus.Failed, message.Status);
            Assert.Equal(2, message.Attempts);

            _transport.Fail = false;
            var processed = await processor.ProcessPendingAsync();

            Assert.Equal(0, processed);
            Assert.Empty(_transport.Published);
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }
}
