namespace Truss.Application
{
    /// <summary>
    /// Dispatches requests to their handlers through the pipeline.
    /// Handlers are resolved from the current dependency injection scope.
    /// </summary>
    public interface IDispatcher
    {
        /// <summary>
        /// Dispatches a request through the pipeline and returns its response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response produced by the handler.</returns>
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    }
}
