using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Truss.Messaging.RabbitMq;
using Truss.Messaging.Transports.Tests.Fakes;
using Xunit;

namespace Truss.Messaging.Transports.Tests
{
    [Collection("rabbitmq")]
    public class RabbitMqTransportTests(RabbitMqFixture fixture)
    {
        private ServiceProvider BuildProvider(string queueName, Action<TrussRabbitMqTransportOptions>? tweak = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ReceivedEvents>();
            services.AddTrussMessaging(options => options.AddAssembly<ItemCreated>());
            services.AddTrussRabbitMqTransport(options =>
            {
                options.ConnectionString = fixture.ConnectionString;
                options.QueueName = queueName;
                options.RetryDelay = TimeSpan.FromMilliseconds(50);
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

        private async Task DeleteQueues(string queueName)
        {
            var factory = new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) };
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await channel.QueueDeleteAsync(queueName);
            await channel.QueueDeleteAsync(queueName + ".dead");
        }

        [Fact]
        public async Task Message_PublishedBeforeConsumerStarts_IsDeliveredAfterwards()
        {
            var queueName = $"truss.test.{Guid.NewGuid():N}";
            var provider = BuildProvider(queueName);
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
                await DeleteQueues(queueName);
            }
        }

        [Fact]
        public async Task FailingHandler_IsRetried_ThenDeadLettered_AndConsumerKeepsWorking()
        {
            var queueName = $"truss.test.{Guid.NewGuid():N}";
            var provider = BuildProvider(queueName, options => options.MaxAttempts = 2);
            await using var _ = provider;

            await StartHostedServices(provider);

            try
            {
                using (var scope = provider.CreateScope())
                {
                    var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
                    await publisher.Publish(new ThrowingEvent());
                }

                var factory = new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) };
                await using var connection = await factory.CreateConnectionAsync();

                await WaitUntil(
                    async () =>
                    {
                        try
                        {
                            await using var channel = await connection.CreateChannelAsync();
                            return await channel.MessageCountAsync(queueName + ".dead") == 1;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    },
                    "The message was not dead-lettered.");

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
                await DeleteQueues(queueName);
            }
        }
    }
}
