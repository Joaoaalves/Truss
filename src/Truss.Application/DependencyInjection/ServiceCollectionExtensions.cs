using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application.Abstractions.Dispatching;
using Truss.Application.Abstractions.Events;
using Truss.Application.Abstractions.Requests;
using Truss.Application.Behaviors;
using Truss.Application.Dispatching;
using Truss.Application.Events;

namespace Truss.Application.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the Truss execution pipeline.
    /// </summary>
    public static class ServiceCollectionExtensions
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

            var assemblies = options.Assemblies.Distinct().ToArray();

            RegisterImplementations(services, assemblies, typeof(IRequestHandler<,>));
            RegisterImplementations(services, assemblies, typeof(IDomainEventHandler<>));
            RegisterImplementations(services, assemblies, typeof(IValidator<>));

            return services;
        }

        /// <summary>
        /// Registers all concrete implementations of a generic interface found in the given assemblies.
        /// </summary>
        private static void RegisterImplementations(IServiceCollection services, Assembly[] assemblies, Type genericInterface)
        {
            var types = assemblies
                .SelectMany(assembly => assembly.GetTypes())
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
