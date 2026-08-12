namespace Truss.Remote
{
    /// <summary>
    /// Thrown when a remote context could not answer a query: the host was
    /// unreachable, the query is not part of its contract, or it failed in a
    /// way that is not a validation or business rule outcome.
    /// </summary>
    public class RemoteContextException(string message, Exception? innerException = null)
        : Exception(message, innerException)
    {
    }
}
