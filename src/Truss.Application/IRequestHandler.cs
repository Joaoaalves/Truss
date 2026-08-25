namespace Truss.Application
{
    /// <summary>
    /// Handles a request and produces its response.
    /// Exactly one handler must exist per request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response produced by the request.</returns>
        Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }
}
