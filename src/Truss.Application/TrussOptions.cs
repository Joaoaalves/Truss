using System.Reflection;

namespace Truss.Application
{
    /// <summary>
    /// Options used to configure Truss registration.
    /// </summary>
    public sealed class TrussOptions
    {
        internal List<Assembly> Assemblies { get; } = [];

        /// <summary>
        /// Adds an assembly to be scanned for handlers and validators.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public void AddAssembly(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            Assemblies.Add(assembly);
        }

        /// <summary>
        /// Adds the assembly containing the marker type to be scanned for handlers and validators.
        /// </summary>
        /// <typeparam name="TMarker">A type contained in the assembly to scan.</typeparam>
        public void AddAssembly<TMarker>()
        {
            Assemblies.Add(typeof(TMarker).Assembly);
        }
    }
}
