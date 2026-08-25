using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truss.Application;
using Truss.Messaging.Tests.Fakes;
using Xunit;
using Truss.Messaging.Outbox;

namespace Truss.Messaging.Tests
{
    /// <summary>
    /// The traceparent captured when an event is published travels on the
    /// envelope, so the consumer's span is a child of the command's trace even
    /// though a transport and an outbox poll loop sit in between.
    /// </summary>
    public class TracePropagationTests : IAsyncLifetime
    {
        private readonly string _databasePath;
        private readonly ServiceProvider _provider;

        public TracePropagationTests()
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
        public async Task TheConsumerSpan_JoinsThePublishersTrace()
        {
            var startedActivities = new ConcurrentBag<Activity>();

            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = startedActivities.Add
            };

            ActivitySource.AddActivityListener(listener);

            using var testSource = new ActivitySource(nameof(TracePropagationTests));
            ActivityTraceId rootTraceId;

            using (var root = testSource.StartActivity("handle request"))
            {
                Assert.NotNull(root);
                rootTraceId = root.TraceId;

                using var scope = _provider.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new CreateItemCommand(Guid.NewGuid()));
            }

            var received = _provider.GetRequiredService<ReceivedEvents>();
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < deadline && received.Snapshot().Count == 0)
                await Task.Delay(25);

            Assert.Single(received.Snapshot());

            using (var scope = _provider.CreateScope())
            {
                var message = scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Set<OutboxMessage>().Single();
                Assert.NotNull(message.TraceParent);
                Assert.Contains(rootTraceId.ToHexString(), message.TraceParent);
            }

            // Other fixtures dispatch messages concurrently and this listener hears
            // every source, so the spans of this test are found by their trace.
            Assert.Contains(startedActivities, activity =>
                activity.OperationName.StartsWith("consume ", StringComparison.Ordinal) && activity.TraceId == rootTraceId);

            Assert.Contains(startedActivities, activity =>
                activity.OperationName.StartsWith("publish ", StringComparison.Ordinal) && activity.TraceId == rootTraceId);
        }

        public async Task DisposeAsync()
        {
            foreach (var hostedService in _provider.GetServices<IHostedService>())
                await hostedService.StopAsync(CancellationToken.None);

            await _provider.DisposeAsync();

            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
    }
}
