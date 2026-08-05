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

            foreach (var behavior in provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var next = pipeline;
                pipeline = () => behavior.Handle((TRequest)request, next, cancellationToken);
            }

            return pipeline();
        }
    }
}
