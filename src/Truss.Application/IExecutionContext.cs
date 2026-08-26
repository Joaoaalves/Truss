namespace Truss.Application
{
    /// <summary>
    /// Ambient information about the current execution, available anywhere in the scope.
    /// The correlation id ties together every log entry and span produced while
    /// handling one request, across commands, domain events and handlers.
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        /// Gets the correlation id of the current execution, or <see cref="Guid.Empty"/> when none is set.
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        /// Gets whether a correlation id is set for the current execution.
        /// </summary>
        bool IsAvailable { get; }
    }
}
