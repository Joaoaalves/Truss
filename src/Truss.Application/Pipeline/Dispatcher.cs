namespace Truss.Application.Pipeline
{
    /// <summary>
    /// Default dispatcher implementation.
    /// Resolves handlers and behaviors from the current dependency injection scope,
    /// caching a typed invoker per request type so reflection happens only once.
    /// </summary>
    public class Dispatcher(IServiceProvider provider) : IDispatcher
    {
        private readonly IServiceProvider _provider = provider;

        /// <inheritdoc />
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var wrapper = (RequestHandlerWrapper<TResponse>)WrapperCache.Requests.GetOrAdd(
                request.GetType(),
                static requestType => (RequestHandlerWrapper)Activator.CreateInstance(
                    typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse)))!
            );

            return wrapper.Handle(request, _provider, cancellationToken);
        }
    }
}
