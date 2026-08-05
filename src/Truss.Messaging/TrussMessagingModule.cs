using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Truss.Messaging;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register Truss messaging.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussMessagingModule
    {
        /// <summary>
        /// Registers the integration event runtime: the type registry, the JSON serializer,
        /// the consumer dispatcher, a direct publisher, and every integration event handler
        /// found in the configured assemblies.
        /// A transport must be registered separately; configure the outbox for transactional publishing.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">The configuration action used to expose event and handler assemblies.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no assembly is registered.</exception>
        public static IServiceCollection AddTrussMessaging(this IServiceCollection services, Action<TrussMessagingOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var options = new TrussMessagingOptions();
            configure(options);

            if (options.Assemblies.Count == 0)
            {
                throw new InvalidOperationException(
                    "At least one assembly must be registered. Use options.AddAssembly<TMarker>() to expose the assemblies that contain your integration events and handlers."
                );
            }

            var assemblyList = GetOrCreateAssemblyList(services);

            services.TryAddSingleton(provider =>
                IntegrationEventTypeRegistry.FromAssemblies(assemblyList.Assemblies));
            services.TryAddSingleton<IIntegrationEventSerializer, JsonIntegrationEventSerializer>();
            services.TryAddSingleton<IIntegrationEventDispatcher, IntegrationEventDispatcher>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<OutboxSignal>();
            services.TryAddScoped<IIntegrationEventPublisher, DirectIntegrationEventPublisher>();

            foreach (var assembly in options.Assemblies.Distinct())
                services.AddTrussMessagingAssembly(assembly);

            return services;
        }

        /// <summary>
        /// Exposes an additional assembly of integration events and handlers to the messaging runtime.
        /// Intended for modules that ship their own events, such as the jobs module,
        /// and for modular monoliths registering events per module.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assembly">The assembly to expose.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussMessagingAssembly(this IServiceCollection services, Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var assemblyList = GetOrCreateAssemblyList(services);

            if (assemblyList.Assemblies.Contains(assembly))
                return services;

            assemblyList.Add(assembly);

            var types = assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract);

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>));

                foreach (var @interface in interfaces)
                {
                    services.AddTransient(@interface, type);
                }
            }

            return services;
        }

        /// <summary>
        /// Registers the in-memory transport and its consumer worker.
        /// Intended for development, tests and modular monoliths; use a durable transport in production.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussInMemoryTransport(this IServiceCollection services)
        {
            services.AddSingleton<InMemoryTransport>();
            services.AddSingleton<IMessageTransport>(provider => provider.GetRequiredService<InMemoryTransport>());
            services.AddHostedService<InMemoryTransportWorker>();

            return services;
        }

        /// <summary>
        /// Finds the assembly list accumulated across module registrations, creating it on first use.
        /// The registry is built lazily from this list, so modules can add assemblies in any order.
        /// </summary>
        private static TrussMessagingAssemblyList GetOrCreateAssemblyList(IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TrussMessagingAssemblyList));

            if (descriptor?.ImplementationInstance is TrussMessagingAssemblyList existing)
                return existing;

            var created = new TrussMessagingAssemblyList();
            services.AddSingleton(created);
            return created;
        }
    }
}
