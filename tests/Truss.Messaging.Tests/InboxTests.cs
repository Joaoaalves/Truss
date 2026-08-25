using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Messaging.Tests.Fakes;
using Xunit;
using Truss.Messaging.Dispatch;
using Truss.Messaging.Inbox;
using Truss.Messaging.Serialization;

namespace Truss.Messaging.Tests
{
    /// <summary>
    /// The inbox is the consumer-side half of exactly-once: a redelivered
    /// message is skipped, and the processing record commits inside the same
    /// unit of work as the handler, so a failed attempt leaves no trace and
    /// the message stays retryable.
    /// </summary>
    public class InboxTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public InboxTests()
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
            services.AddTrussInbox<MessagingDbContext>();

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.EnsureCreated();
        }

        private IntegrationEventEnvelope Envelope(IIntegrationEvent integrationEvent)
        {
            return _provider.GetRequiredService<IIntegrationEventSerializer>().Serialize(integrationEvent);
        }

        [Fact]
        public async Task ARedeliveredMessage_IsHandledOnce()
        {
            var dispatcher = _provider.GetRequiredService<IIntegrationEventDispatcher>();
            var envelope = Envelope(new ItemCreated(Guid.NewGuid()));

            await dispatcher.Dispatch(envelope);
            await dispatcher.Dispatch(envelope);

            Assert.Single(_provider.GetRequiredService<ReceivedEvents>().Snapshot());

            using var scope = _provider.CreateScope();
            var record = scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Set<InboxRecord>().Single();
            Assert.Equal(envelope.MessageId, record.MessageId);
            Assert.Equal(envelope.Name, record.Name);
        }

        [Fact]
        public async Task AFailedAttempt_LeavesNoInboxRecord_SoTheRetryIsNotMistakenForADuplicate()
        {
            FlakyEventHandler.Reset();
            var dispatcher = _provider.GetRequiredService<IIntegrationEventDispatcher>();
            var envelope = Envelope(new FlakyEvent(Guid.NewGuid()));

            await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.Dispatch(envelope));

            using (var scope = _provider.CreateScope())
            {
                Assert.Empty(scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Set<InboxRecord>());
            }

            await dispatcher.Dispatch(envelope);
            Assert.Single(_provider.GetRequiredService<ReceivedEvents>().Snapshot());

            await dispatcher.Dispatch(envelope);
            Assert.Single(_provider.GetRequiredService<ReceivedEvents>().Snapshot());
        }

        [Fact]
        public async Task TheSweep_DeletesOnlyRecordsPastTheirRetention()
        {
            var dispatcher = _provider.GetRequiredService<IIntegrationEventDispatcher>();
            await dispatcher.Dispatch(Envelope(new ItemCreated(Guid.NewGuid())));

            using var scope = _provider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IInboxStore>();

            Assert.Equal(0, await store.DeleteProcessedBefore(DateTimeOffset.UtcNow.AddMinutes(-5)));
            Assert.Equal(1, await store.DeleteProcessedBefore(DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }
}
