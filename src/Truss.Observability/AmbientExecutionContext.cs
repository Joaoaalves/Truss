using Truss.Application;

namespace Truss.Observability
{
    internal sealed class AmbientExecutionContext : IExecutionContext
    {
        public string CorrelationId => ExecutionContextHolder.Current ?? string.Empty;

        public bool IsAvailable => ExecutionContextHolder.Current is not null;
    }
}
