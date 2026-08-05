namespace Truss.Messaging
{
    /// <summary>
    /// Exception thrown when an integration event type or wire name is not present in the registry.
    /// </summary>
    public class UnknownIntegrationEventException(string message) : Exception(message)
    {
    }
}
