namespace Truss.Application
{
    /// <summary>
    /// Represents a read-only operation that produces a result.
    /// Queries never change state and never participate in a unit of work.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    public interface IQuery<TResult> : IRequest<TResult>
    {
    }
}
