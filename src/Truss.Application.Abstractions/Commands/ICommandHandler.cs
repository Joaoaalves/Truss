using Truss.Application.Abstractions.Requests;

namespace Truss.Application.Abstractions.Commands
{
    /// <summary>
    /// Handles a command that produces a result.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
    }

    /// <summary>
    /// Handles a command that does not produce a result.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
        where TCommand : ICommand
    {
    }
}
