using System.Reflection;
using Truss.Application;
using Truss.Remote;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides the registration of a remote context.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so
    /// registration is available in the composition root without additional usings.
    /// </summary>
    public static class TrussRemoteModule
    {
        /// <summary>
        /// Declares that a context lives in another service and wires its
        /// queries to cross the network. Every IQuery in the contracts assembly
        /// gets a forwarding handler, so callers keep dispatching as always
        /// while the composition root shows, explicitly, that the answer comes
        /// from somewhere else, with the timeout beside it.
        /// Commands are deliberately not wired: a synchronous command between
        /// contexts is coupling in disguise; publish an integration event.
        /// </summary>
        /// <typeparam name="TContractsMarker">Any type of the context's contracts assembly.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="name">The context's name, used to name its HttpClient (truss-remote-{name}).</param>
        /// <param name="baseAddress">Where the context's host answers.</param>
        /// <param name="configure">Optional configuration of prefix and timeout.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddRemoteContext<TContractsMarker>(
            this IServiceCollection services,
            string name,
            Uri baseAddress,
            Action<RemoteContextOptions>? configure = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(baseAddress);

            var options = new RemoteContextOptions { BaseAddress = baseAddress };
            configure?.Invoke(options);

            var clientName = $"truss-remote-{name}";

            services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
            });

            foreach (var (query, result) in Queries(typeof(TContractsMarker).Assembly))
            {
                var contract = typeof(IRequestHandler<,>).MakeGenericType(query, result);
                var forwarder = typeof(RemoteQueryHandler<,>).MakeGenericType(query, result);

                services.AddTransient(contract, provider => Activator.CreateInstance(
                    forwarder,
                    provider.GetRequiredService<IHttpClientFactory>(),
                    name,
                    clientName,
                    options.Prefix)!);
            }

            return services;
        }

        internal static IEnumerable<(Type Query, Type Result)> Queries(Assembly contracts)
        {
            foreach (var type in contracts.GetTypes().Where(type => type.IsClass && !type.IsAbstract && !type.IsGenericType))
            {
                foreach (var contract in type.GetInterfaces())
                {
                    if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IQuery<>))
                        yield return (type, contract.GetGenericArguments()[0]);
                }
            }
        }
    }
}
