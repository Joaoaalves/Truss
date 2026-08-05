namespace Truss.Messaging
{
    /// <summary>
    /// Declares the stable wire name and version of an integration event.
    /// The name travels with the serialized event, so renaming the CLR type does not break consumers.
    /// Increase the version when the payload shape changes; each version maps to its own CLR type.
    /// </summary>
    /// <param name="name">The stable wire name of the event, for example "orders.order-placed".</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class IntegrationEventNameAttribute(string name) : Attribute
    {
        /// <summary>
        /// Gets the stable wire name of the event.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets or sets the version of the event contract. Defaults to 1.
        /// </summary>
        public int Version { get; init; } = 1;
    }
}
