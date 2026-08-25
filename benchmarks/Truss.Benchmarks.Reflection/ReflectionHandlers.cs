using Truss.Application;

namespace Truss.Benchmarks.Reflection
{
    // This assembly deliberately does NOT reference the Truss.Generators
    // analyzer, so registering it exercises the runtime reflection scan, the
    // path every assembly took before compile-time registration existed.
    public sealed record ReflectionPing(string Value) : ICommand<string>;

    public class ReflectionPingHandler : ICommandHandler<ReflectionPing, string>
    {
        public Task<string> Handle(ReflectionPing request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Value);
        }
    }
}
