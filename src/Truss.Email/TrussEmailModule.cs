using Truss.Email;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the built-in email senders.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussEmailModule
    {
        /// <summary>
        /// Registers the console sender, which prints messages to the log
        /// instead of delivering them. Meant for development.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussConsoleEmail(this IServiceCollection services)
        {
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();

            return services;
        }
    }
}
