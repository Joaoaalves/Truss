using Microsoft.Extensions.DependencyInjection;

namespace Truss.Application
{
    internal abstract class RequestHandlerWrapper
    {
    }

    internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapper
    {
        public abstract Task<TResponse> Handle(
            IRequest<TResponse> request,
            IServiceProvider provider,
            CancellationToken cancellationToken);
    }

    internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> Handle(
            IRequest<TResponse> request,
            IServiceProvider provider,
            CancellationToken cancellationToken)
        {
            var handler = provider.GetService<IRequestHandler<TRequest, TResponse>>()
                ?? throw new InvalidOperationException(
                    $"No handler is registered for request type {request.GetType().Name}."
                );

            RequestHandlerDelegate<TResponse> pipeline =
                () => handler.Handle((TRequest)request, cancellationToken);

            // The container answers IEnumerable with an indexable array, so the
            // chain is built walking it backwards, without a LINQ Reverse
            // buffering a copy on every dispatch.
            var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

            if (behaviors is IList<IPipelineBehavior<TRequest, TResponse>> list)
            {
                for (var index = list.Count - 1; index >= 0; index--)
                {
                    var behavior = list[index];
                    var next = pipeline;
                    pipeline = () => behavior.Handle((TRequest)request, next, cancellationToken);
                }
            }
            else
            {
                foreach (var behavior in behaviors.Reverse())
                {
                    var next = pipeline;
                    pipeline = () => behavior.Handle((TRequest)request, next, cancellationToken);
                }
            }

            return pipeline();
        }
    }
}
