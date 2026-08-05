namespace Truss.Application.Abstractions.Requests
{
    /// <summary>
    /// Represents the continuation of the request pipeline.
    /// Invoking it executes the next behavior or the request handler itself.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

    /// <summary>
    /// Middleware-style hook that wraps the execution of a request handler.
    /// Behaviors execute in registration order: the first registered behavior is the outermost.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Wraps the execution of the next step in the pipeline.
        /// </summary>
        /// <param name="request">The request being dispatched.</param>
        /// <param name="next">The continuation of the pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response produced by the pipeline.</returns>
        Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
    }
}
