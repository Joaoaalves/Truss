using System.Reflection;
using FluentValidation;
using Truss.Application;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the Truss execution pipeline.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussApplicationModule
    {
        /// <summary>
        /// Registers the dispatcher, the validation pipeline and every handler and validator
        /// found in the configured assemblies.
        /// Assemblies must be registered explicitly; there is no implicit discovery.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">The configuration action used to expose handler assemblies.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no assembly is registered.</exception>
        public static IServiceCollection AddTruss(this IServiceCollection services, Action<TrussOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var options = new TrussOptions();
            configure(options);

            if (options.Assemblies.Count == 0)
            {
                throw new InvalidOperationException(
                    "At least one assembly must be registered. Use options.AddAssembly<TMarker>() to expose the assemblies that contain your handlers."
                );
            }

            services.AddScoped<IDispatcher, Dispatcher>();
            services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            foreach (var assembly in options.Assemblies.Distinct())
            {
                if (TrussGeneratedRegistry.TryGetRegistration(assembly, out var registration))
                {
                    registration(services);
                    continue;
                }

                RegisterImplementations(services, assembly, typeof(IRequestHandler<,>));
                RegisterImplementations(services, assembly, typeof(IDomainEventHandler<>));
                RegisterImplementations(services, assembly, typeof(IValidator<>));
            }

            return services;
        }

        /// <summary>
        /// Registers all concrete implementations of a generic interface found in the given assembly.
        /// Used as the fallback when no compile-time registration exists for the assembly.
        /// </summary>
        private static void RegisterImplementations(IServiceCollection services, Assembly assembly, Type genericInterface)
        {
            var types = assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract);

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface);

                foreach (var @interface in interfaces)
                {
                    services.AddTransient(@interface, type);
                }
            }
        }
    }
}
