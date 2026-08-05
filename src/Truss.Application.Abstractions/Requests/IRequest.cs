namespace Truss.Application.Abstractions.Requests
{
    /// <summary>
    /// Represents a request that produces a response when dispatched.
    /// Commands and queries are specializations of this contract.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IRequest<TResponse>
    {
    }
}
