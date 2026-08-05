using System.Reflection;

namespace Truss.Messaging
{
    internal sealed class TrussMessagingAssemblyList
    {
        private readonly List<Assembly> _assemblies = [];

        public IReadOnlyList<Assembly> Assemblies => _assemblies;

        public void Add(Assembly assembly)
        {
            if (!_assemblies.Contains(assembly))
                _assemblies.Add(assembly);
        }
    }
}
