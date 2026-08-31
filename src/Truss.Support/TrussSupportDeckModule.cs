using Microsoft.Extensions.Options;
using Truss.Support;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register the deck client.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussSupportDeckModule
    {
        /// <summary>
        /// Registers the typed client for the deck's ingestion API.
        /// Options can be configured here or bound from the "Truss:Support:Deck"
        /// configuration section; the ApiKey comes from the environment.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the connection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussSupportDeck(
            this IServiceCollection services,
            Action<TrussSupportDeckOptions>? configure = null)
        {
            services.AddOptions<TrussSupportDeckOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.AddHttpClient<ISupportDeckClient, SupportDeckClient>((provider, http) =>
            {
                var options = provider.GetRequiredService<IOptions<TrussSupportDeckOptions>>().Value;
                options.Validate();

                http.BaseAddress = new Uri(options.Url.EndsWith('/') ? options.Url : options.Url + "/");
                http.DefaultRequestHeaders.Add("X-Deck-Key", options.ApiKey);
            });

            return services;
        }
    }
}
