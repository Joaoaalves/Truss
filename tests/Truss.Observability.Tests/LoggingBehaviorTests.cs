using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Truss.Application;
using Truss.Observability.Tests.Fakes;
using Xunit;
using Truss.Application.Pipeline;

namespace Truss.Observability.Tests
{
    public class LoggingBehaviorTests
    {
        private static (ServiceProvider Provider, CapturingLoggerProvider Logs) BuildProvider()
        {
            var logs = new CapturingLoggerProvider();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(logs));
            services.AddSingleton<CorrelationRecorder>();
            services.AddTruss(options => options.AddAssembly<PingCommand>());
            services.AddTrussObservability();

            return (services.BuildServiceProvider(), logs);
        }

        private static async Task<IDispatcher> Dispatcher(ServiceProvider provider)
        {
            var scope = provider.CreateAsyncScope();
            await Task.CompletedTask;
            return scope.ServiceProvider.GetRequiredService<IDispatcher>();
        }

        [Fact]
        public async Task SuccessfulRequest_LogsHandledWithDuration()
        {
            var (provider, logs) = BuildProvider();
            await using var _ = provider;

            await (await Dispatcher(provider)).Send(new PingCommand("abc"));

            Assert.Contains(logs.Logs, log =>
                log.Level == LogLevel.Information && log.Message.Contains("Handled PingCommand"));
        }

        [Fact]
        public async Task FailedRequest_LogsError()
        {
            var (provider, logs) = BuildProvider();
            await using var _ = provider;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => (Dispatcher(provider)).Result.Send(new ThrowingCommand())
            );

            Assert.Contains(logs.Logs, log =>
                log.Level == LogLevel.Error && log.Message.Contains("Failed ThrowingCommand"));
        }

        [Fact]
        public async Task RejectedRequest_LogsWarning_EvenThoughObservabilityWasRegisteredLast()
        {
            var (provider, logs) = BuildProvider();
            await using var _ = provider;

            await Assert.ThrowsAsync<RequestValidationException>(
                () => (Dispatcher(provider)).Result.Send(new PingCommand(""))
            );

            Assert.Contains(logs.Logs, log =>
                log.Level == LogLevel.Warning && log.Message.Contains("Rejected PingCommand"));
        }

        [Fact]
        public async Task Dispatch_CreatesAmbientCorrelation_WhenNoneExists()
        {
            var (provider, _) = BuildProvider();
            await using var __ = provider;
            ExecutionContextHolder.Current = null;

            await (await Dispatcher(provider)).Send(new RecordCorrelationCommand());

            var recorder = provider.GetRequiredService<CorrelationRecorder>();
            var observed = Assert.Single(recorder.Observed);
            Assert.NotEqual(Guid.Empty, Guid.Parse(observed));
        }
    }
}
