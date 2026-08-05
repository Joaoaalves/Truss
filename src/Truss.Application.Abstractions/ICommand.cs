namespace Truss.Application
{
    /// <summary>
    /// Represents a state-changing operation that produces a result.
    /// Commands always execute inside a unit of work when persistence is configured.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    public interface ICommand<TResult> : IRequest<TResult>
    {
    }

    /// <summary>
    /// Represents a state-changing operation that does not produce a result.
    /// </summary>
    public interface ICommand : ICommand<Unit>
    {
    }
}
