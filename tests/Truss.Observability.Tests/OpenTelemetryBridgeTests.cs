using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Truss.Observability.Tests
{
    public class OpenTelemetryBridgeTests
    {
        [Fact]
        public void AddTrussOpenTelemetry_RegistersTracingAndMetrics()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTrussOpenTelemetry(options => options.ServiceName = "bridge-test");

            using var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<TracerProvider>());
            Assert.NotNull(provider.GetService<MeterProvider>());
        }

        [Fact]
        public void DisabledSignals_AreNotRegistered()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTrussOpenTelemetry(options =>
            {
                options.EnableTracing = false;
                options.EnableMetrics = false;
                options.EnableLogging = false;
            });

            using var provider = services.BuildServiceProvider();

            Assert.Null(provider.GetService<TracerProvider>());
            Assert.Null(provider.GetService<MeterProvider>());
        }
    }
}
