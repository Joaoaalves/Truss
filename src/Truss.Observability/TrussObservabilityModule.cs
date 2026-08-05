using Microsoft.Extensions.DependencyInjection.Extensions;
using Truss.Application;
using Truss.Observability;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register Truss observability.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussObservabilityModule
    {
        /// <summary>
        /// Registers the logging and tracing behaviors and the ambient execution context.
        /// The behaviors are placed at the outermost position of the pipeline regardless
        /// of registration order, so validation failures and unit of work outcomes are observed too.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the module.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussObservability(
            this IServiceCollection services,
            Action<TrussObservabilityOptions>? configure = null)
        {
            var options = new TrussObservabilityOptions();
            configure?.Invoke(options);

            services.TryAddSingleton<IExecutionContext, AmbientExecutionContext>();

            if (options.EnableLogging)
                InsertOutermost(services, typeof(LoggingBehavior<,>));

            if (options.EnableTracing)
                InsertOutermost(services, typeof(TracingBehavior<,>));

            return services;
        }

        /// <summary>
        /// Inserts a behavior before every other pipeline behavior already registered,
        /// making it the outermost. The first registered behavior wraps all the others.
        /// </summary>
        private static void InsertOutermost(IServiceCollection services, Type behavior)
        {
            var descriptor = ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), behavior);

            for (var i = 0; i < services.Count; i++)
            {
                if (services[i].ServiceType == typeof(IPipelineBehavior<,>))
                {
                    services.Insert(i, descriptor);
                    return;
                }
            }

            services.Add(descriptor);
        }
    }
}
