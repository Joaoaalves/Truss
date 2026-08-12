using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truss.Application;
using Truss.Messaging.Tests.Fakes;
using Xunit;

namespace Truss.Messaging.Tests
{
    /// <summary>
    /// The outbox reports its operability through the "Truss.Messaging" meter:
    /// counters and lag as messages flow, depth gauges sampled by the processor.
    /// </summary>
    public class OutboxMetricsTests : IAsyncLifetime
    {
        private readonly string _databasePath;
        private readonly ServiceProvider _provider;

        public OutboxMetricsTests()
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
                options.StatisticsInterval = TimeSpan.Zero;
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
        public async Task PublishedMessages_ShowUpInTheMeter()
        {
            var meter = _provider.GetRequiredService<OutboxMetrics>().Meter;
            var counters = new ConcurrentDictionary<string, long>();
            var lagSamples = new ConcurrentBag<double>();

            using var listener = new MeterListener();

            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter == meter)
                    meterListener.EnableMeasurementEvents(instrument);
            };

            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                // Gauges report the current value on every observation; only the
                // counters accumulate.
                if (instrument is ObservableGauge<long>)
                    counters[instrument.Name] = measurement;
                else
                    counters.AddOrUpdate(instrument.Name, measurement, (_, total) => total + measurement);
            });

            listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
                lagSamples.Add(measurement));

            listener.Start();

            using (var scope = _provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new CreateItemCommand(Guid.NewGuid()));
            }

            var received = _provider.GetRequiredService<ReceivedEvents>();
            var deadline = DateTime.UtcNow.AddSeconds(30);

            // Publication and consumption finish on their own schedules; wait
            // for both before asserting anything.
            while (DateTime.UtcNow < deadline
                && (received.Snapshot().Count == 0 || !counters.ContainsKey("truss.outbox.published")))
            {
                listener.RecordObservableInstruments();
                await Task.Delay(25);
            }

            Assert.Single(received.Snapshot());
            Assert.Equal(1, counters["truss.outbox.published"]);
            Assert.NotEmpty(lagSamples);
            Assert.All(lagSamples, lag => Assert.True(lag >= 0));

            // The gauges answer once the processor sampled the store; with the
            // message published, nothing is pending or dead lettered.
            var gaugeDeadline = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < gaugeDeadline && !counters.ContainsKey("truss.outbox.pending"))
            {
                listener.RecordObservableInstruments();
                await Task.Delay(25);
            }

            Assert.Equal(0, counters["truss.outbox.pending"]);
            Assert.Equal(0, counters["truss.outbox.dead_lettered"]);
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
