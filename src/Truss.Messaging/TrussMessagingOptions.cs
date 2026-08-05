using System.Reflection;

namespace Truss.Messaging
{
    /// <summary>
    /// Options used to configure Truss messaging registration.
    /// </summary>
    public sealed class TrussMessagingOptions
    {
        internal List<Assembly> Assemblies { get; } = [];

        /// <summary>
        /// Adds an assembly to be scanned for integration event types and handlers.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public void AddAssembly(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            Assemblies.Add(assembly);
        }

        /// <summary>
        /// Adds the assembly containing the marker type to be scanned for integration event types and handlers.
        /// </summary>
        /// <typeparam name="TMarker">A type contained in the assembly to scan.</typeparam>
        public void AddAssembly<TMarker>()
        {
            Assemblies.Add(typeof(TMarker).Assembly);
        }
    }
}
