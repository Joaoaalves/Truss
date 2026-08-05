using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Truss.Messaging.Postgres;
using Truss.Messaging.Transports.Tests.Fakes;
using Xunit;

namespace Truss.Messaging.Transports.Tests
{
    [Collection("postgres")]
    public class PostgresTransportTests(PostgresFixture fixture)
    {
        private async Task Truncate()
        {
            await using var connection = new NpgsqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();

            try
            {
                await using var command = new NpgsqlCommand(
                    "TRUNCATE truss_messages; TRUNCATE truss_messages_dead;", connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
            {
            }
        }

        private ServiceProvider BuildProvider(Action<TrussPostgresTransportOptions>? tweak = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ReceivedEvents>();
            services.AddTrussMessaging(options => options.AddAssembly<ItemCreated>());
            services.AddTrussPostgresTransport(options =>
            {
                options.ConnectionString = fixture.ConnectionString;
                options.PollingInterval = TimeSpan.FromMilliseconds(200);
                options.RetryBaseDelay = TimeSpan.FromMilliseconds(50);
                tweak?.Invoke(options);
            });

            return services.BuildServiceProvider();
        }

        private static async Task StartHostedServices(ServiceProvider provider)
        {
            foreach (var hostedService in provider.GetServices<IHostedService>())
                await hostedService.StartAsync(CancellationToken.None);
        }

        private static async Task StopHostedServices(ServiceProvider provider)
        {
            foreach (var hostedService in provider.GetServices<IHostedService>())
                await hostedService.StopAsync(CancellationToken.None);
        }

        private static async Task WaitUntil(Func<Task<bool>> condition, string failure)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);

            while (DateTime.UtcNow < deadline)
            {
                if (await condition())
                    return;

                await Task.Delay(50);
            }

            Assert.Fail(failure);
        }

        [Fact]
        public async Task Message_PublishedBeforeConsumerStarts_IsDeliveredAfterwards()
        {
            await Truncate();
            var provider = BuildProvider();
            await using var _ = provider;

            var itemId = Guid.NewGuid();

            using (var scope = provider.CreateScope())
            {
                var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
                await publisher.Publish(new ItemCreated(itemId));
            }

            await StartHostedServices(provider);

            try
            {
                var received = provider.GetRequiredService<ReceivedEvents>();
                await WaitUntil(
                    () => Task.FromResult(received.Snapshot().OfType<ItemCreated>().Any(e => e.ItemId == itemId)),
                    "The message was not delivered.");
            }
            finally
            {
                await StopHostedServices(provider);
            }
        }

        [Fact]
        public async Task FailingHandler_IsRetried_ThenDeadLettered_AndConsumerKeepsWorking()
        {
            await Truncate();
            var provider = BuildProvider(options => options.MaxAttempts = 2);
            await using var _ = provider;

            await StartHostedServices(provider);

            try
            {
                using (var scope = provider.CreateScope())
                {
                    var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
                    await publisher.Publish(new ThrowingEvent());
                }

                await WaitUntil(async () => await CountDeadLetters() == 1, "The message was not dead-lettered.");

                await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
                {
                    await connection.OpenAsync();
                    await using var pending = new NpgsqlCommand("SELECT count(*) FROM truss_messages", connection);
                    Assert.Equal(0L, await pending.ExecuteScalarAsync());
                    await using var attempts = new NpgsqlCommand("SELECT attempts FROM truss_messages_dead", connection);
                    Assert.Equal(2, await attempts.ExecuteScalarAsync());
                }

                var itemId = Guid.NewGuid();

                using (var scope = provider.CreateScope())
                {
                    var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
                    await publisher.Publish(new ItemCreated(itemId));
                }

                var received = provider.GetRequiredService<ReceivedEvents>();
                await WaitUntil(
                    () => Task.FromResult(received.Snapshot().OfType<ItemCreated>().Any(e => e.ItemId == itemId)),
                    "The consumer stopped working after a dead-letter.");
            }
            finally
            {
                await StopHostedServices(provider);
            }
        }

        private async Task<long> CountDeadLetters()
        {
            await using var connection = new NpgsqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT count(*) FROM truss_messages_dead", connection);
            return (long)(await command.ExecuteScalarAsync())!;
        }
    }
}
