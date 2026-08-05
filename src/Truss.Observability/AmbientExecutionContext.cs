using Truss.Application;

namespace Truss.Observability
{
    internal sealed class AmbientExecutionContext : IExecutionContext
    {
        public Guid CorrelationId => ExecutionContextHolder.Current ?? Guid.Empty;

        public bool IsAvailable => ExecutionContextHolder.Current.HasValue;
    }
}
