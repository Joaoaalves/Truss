using System.Collections.Concurrent;

namespace Truss.Application.Pipeline
{
    internal static class WrapperCache
    {
        internal static readonly ConcurrentDictionary<Type, RequestHandlerWrapper> Requests = new();

        internal static readonly ConcurrentDictionary<Type, DomainEventHandlerWrapper> DomainEvents = new();
    }
}
