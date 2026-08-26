using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Truss.Application;

namespace Truss.Observability.Tests.Fakes
{
    public sealed record PingCommand(string Value) : ICommand<string>;

    public class PingCommandHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> Handle(PingCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"pong:{request.Value}");
        }
    }

    public class PingCommandValidator : AbstractValidator<PingCommand>
    {
        public PingCommandValidator()
        {
            RuleFor(command => command.Value).NotEmpty();
        }
    }

    public sealed record ThrowingCommand : ICommand;

    public sealed record TracedCommand : ICommand;

    public class TracedCommandHandler : ICommandHandler<TracedCommand>
    {
        public Task<Unit> Handle(TracedCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Unit.Value);
        }
    }

    public sealed record TracedThrowingCommand : ICommand;

    public class TracedThrowingCommandHandler : ICommandHandler<TracedThrowingCommand>
    {
        public Task<Unit> Handle(TracedThrowingCommand request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Handler failed.");
        }
    }

    public class ThrowingCommandHandler : ICommandHandler<ThrowingCommand>
    {
        public Task<Unit> Handle(ThrowingCommand request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Handler failed.");
        }
    }

    public class CorrelationRecorder
    {
        public ConcurrentQueue<string> Observed { get; } = new();
    }

    public sealed record RecordCorrelationCommand : ICommand;

    public class RecordCorrelationCommandHandler(IExecutionContext executionContext, CorrelationRecorder recorder)
        : ICommandHandler<RecordCorrelationCommand>
    {
        public Task<Unit> Handle(RecordCorrelationCommand request, CancellationToken cancellationToken)
        {
            recorder.Observed.Enqueue(executionContext.CorrelationId);
            return Task.FromResult(Unit.Value);
        }
    }

    public sealed record CapturedLog(LogLevel Level, string Message, string Category);

    public sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<CapturedLog> Logs { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Logs);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                logs.Enqueue(new CapturedLog(logLevel, formatter(state, exception), category));
            }
        }
    }
}
