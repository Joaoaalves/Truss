namespace Truss.Application.Pipeline
{
    /// <summary>
    /// Pipeline behavior that commits the unit of work after a command handler succeeds.
    /// If the handler throws, nothing is committed and the exception propagates unchanged.
    /// Applies to commands only; queries never trigger a commit.
    /// </summary>
    /// <typeparam name="TRequest">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    public class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : ICommand<TResponse>
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        /// <inheritdoc />
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var response = await next();
            await _unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
    }
}
