using System.Reflection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Truss.Observability.OpenTelemetry;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the OpenTelemetry bridge.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussOpenTelemetryModule
    {
        /// <summary>
        /// Exports the Truss activity sources, the Truss meter and the application logs
        /// over OTLP, together with the ASP.NET Core and HttpClient instrumentation.
        /// The destination comes from the options or from the standard environment
        /// variables, so any OTLP dashboard works: the Aspire dashboard, Grafana, Seq.
        /// Use it next to AddTrussObservability, which produces the signals this exports.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the bridge.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussOpenTelemetry(
            this IServiceCollection services,
            Action<TrussOpenTelemetryOptions>? configure = null)
        {
            var options = new TrussOpenTelemetryOptions();
            configure?.Invoke(options);

            var serviceName = options.ServiceName
                ?? Assembly.GetEntryAssembly()?.GetName().Name
                ?? "truss-app";

            var builder = services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName));

            if (options.EnableTracing)
            {
                builder.WithTracing(tracing => tracing
                    .AddSource("Truss.Application", "Truss.Messaging", "Truss.Jobs")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(exporter =>
                    {
                        if (options.OtlpEndpoint is { } endpoint)
                            exporter.Endpoint = endpoint;
                    }));
            }

            if (options.EnableMetrics)
            {
                builder.WithMetrics(metrics => metrics
                    .AddMeter("Truss")
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(exporter =>
                    {
                        if (options.OtlpEndpoint is { } endpoint)
                            exporter.Endpoint = endpoint;
                    }));
            }

            if (options.EnableLogging)
            {
                services.Configure<OpenTelemetryLoggerOptions>(logger =>
                {
                    logger.IncludeFormattedMessage = true;
                    logger.IncludeScopes = true;
                });

                builder.WithLogging(logging => logging.AddOtlpExporter(exporter =>
                {
                    if (options.OtlpEndpoint is { } endpoint)
                        exporter.Endpoint = endpoint;
                }));
            }

            return services;
        }
    }
}
