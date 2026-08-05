using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Truss.Messaging.Redis;
using Truss.Messaging.Transports.Tests.Fakes;
using Xunit;

namespace Truss.Messaging.Transports.Tests
{
    [Collection("redis")]
    public class RedisTransportTests(RedisFixture fixture)
    {
        private ServiceProvider BuildProvider(string streamKey, Action<TrussRedisTransportOptions>? tweak = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ReceivedEvents>();
            services.AddTrussMessaging(options => options.AddAssembly<ItemCreated>());
            services.AddTrussRedisTransport(options =>
            {
                options.ConnectionString = fixture.ConnectionString;
                options.StreamKey = streamKey;
                options.PollingInterval = TimeSpan.FromMilliseconds(100);
                options.ReclaimIdleAfter = TimeSpan.FromMilliseconds(200);
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
            var streamKey = $"truss:test:{Guid.NewGuid():N}";
            var provider = BuildProvider(streamKey);
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
            var streamKey = $"truss:test:{Guid.NewGuid():N}";
            var provider = BuildProvider(streamKey, options => options.MaxAttempts = 2);
            await using var _ = provider;

            await StartHostedServices(provider);

            try
            {
                using (var scope = provider.CreateScope())
                {
                    var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
                    await publisher.Publish(new ThrowingEvent());
                }

                var redis = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
                var database = redis.GetDatabase();

                await WaitUntil(
                    async () => await database.StreamLengthAsync(streamKey + ":dead") == 1,
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

                await redis.DisposeAsync();
            }
            finally
            {
                await StopHostedServices(provider);
            }
        }
    }
}
