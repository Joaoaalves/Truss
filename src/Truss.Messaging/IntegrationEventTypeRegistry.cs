using System.Reflection;

namespace Truss.Messaging
{
    /// <summary>
    /// Describes the wire identity of an integration event type.
    /// </summary>
    /// <param name="Name">The stable wire name of the event.</param>
    /// <param name="Version">The version of the event contract.</param>
    public sealed record IntegrationEventDescriptor(string Name, int Version);

    /// <summary>
    /// Maps integration event types to their wire name and version, and back.
    /// Built once at startup from the assemblies registered in the messaging module.
    /// </summary>
    public sealed class IntegrationEventTypeRegistry
    {
        private readonly Dictionary<Type, IntegrationEventDescriptor> _byType = [];
        private readonly Dictionary<(string Name, int Version), Type> _byName = [];

        /// <summary>
        /// Builds a registry from every concrete integration event type found in the given assemblies.
        /// An assembly with a compile-time registration contributes its generated
        /// type list instead of being scanned.
        /// Types carrying <see cref="IntegrationEventNameAttribute"/> use its name and version;
        /// other types default to their full CLR name and version 1.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan for integration event types.</param>
        /// <returns>The populated registry.</returns>
        /// <exception cref="InvalidOperationException">Thrown when two types share the same name and version.</exception>
        public static IntegrationEventTypeRegistry FromAssemblies(IEnumerable<Assembly> assemblies)
        {
            var registry = new IntegrationEventTypeRegistry();

            var eventTypes = assemblies
                .Distinct()
                .SelectMany(assembly => TrussMessagingGeneratedRegistry.TryGetEventTypes(assembly, out var generated)
                    ? generated
                    : assembly.GetTypes())
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IIntegrationEvent).IsAssignableFrom(type));

            foreach (var type in eventTypes)
            {
                var attribute = type.GetCustomAttribute<IntegrationEventNameAttribute>();
                var descriptor = attribute is null
                    ? new IntegrationEventDescriptor(type.FullName!, 1)
                    : new IntegrationEventDescriptor(attribute.Name, attribute.Version);

                if (registry._byName.TryGetValue((descriptor.Name, descriptor.Version), out var existing))
                {
                    throw new InvalidOperationException(
                        $"Integration event name '{descriptor.Name}' version {descriptor.Version} is declared by both {existing.FullName} and {type.FullName}."
                    );
                }

                registry._byType[type] = descriptor;
                registry._byName[(descriptor.Name, descriptor.Version)] = type;
            }

            return registry;
        }

        /// <summary>
        /// Returns the wire descriptor of an integration event type.
        /// </summary>
        /// <param name="eventType">The CLR type of the event.</param>
        /// <exception cref="UnknownIntegrationEventException">Thrown when the type was not registered.</exception>
        public IntegrationEventDescriptor DescriptorFor(Type eventType)
        {
            if (_byType.TryGetValue(eventType, out var descriptor))
                return descriptor;

            throw new UnknownIntegrationEventException(
                $"Integration event type {eventType.FullName} is not registered. Expose its assembly with options.AddAssembly<TMarker>() when calling AddTrussMessaging."
            );
        }

        /// <summary>
        /// Resolves the CLR type registered for a wire name and version.
        /// </summary>
        /// <param name="name">The stable wire name of the event.</param>
        /// <param name="version">The version of the event contract.</param>
        /// <exception cref="UnknownIntegrationEventException">Thrown when no type is registered for the pair.</exception>
        public Type Resolve(string name, int version)
        {
            if (_byName.TryGetValue((name, version), out var type))
                return type;

            throw new UnknownIntegrationEventException(
                $"No integration event type is registered for name '{name}' version {version}."
            );
        }
    }
}
