using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Truss.Messaging.Dispatch
{
    /// <summary>
    /// Receives the messaging registrations produced at compile time by the
    /// Truss.Generators package: the handler registrations and the integration
    /// event types of an assembly, replacing the runtime scan for it.
    /// Not intended to be called from application code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class TrussMessagingGeneratedRegistry
    {
        private static readonly ConcurrentDictionary<Assembly, Action<IServiceCollection>> Handlers = new();
        private static readonly ConcurrentDictionary<Assembly, Type[]> EventTypes = new();

        /// <summary>
        /// Stores the generated messaging registration for an assembly.
        /// </summary>
        /// <param name="assembly">The assembly the registration was generated for.</param>
        /// <param name="registration">The generated handler registration action.</param>
        /// <param name="integrationEventTypes">The integration event types declared in the assembly.</param>
        public static void RegisterAssembly(Assembly assembly, Action<IServiceCollection> registration, Type[] integrationEventTypes)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            ArgumentNullException.ThrowIfNull(registration);
            ArgumentNullException.ThrowIfNull(integrationEventTypes);

            Handlers[assembly] = registration;
            EventTypes[assembly] = integrationEventTypes;
        }

        internal static bool TryGetHandlers(Assembly assembly, [NotNullWhen(true)] out Action<IServiceCollection>? registration)
        {
            return Handlers.TryGetValue(assembly, out registration);
        }

        internal static bool TryGetEventTypes(Assembly assembly, [NotNullWhen(true)] out Type[]? types)
        {
            return EventTypes.TryGetValue(assembly, out types);
        }
    }
}
