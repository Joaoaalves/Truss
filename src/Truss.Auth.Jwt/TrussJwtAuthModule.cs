using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Truss.Auth;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register JWT authentication.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussJwtAuthModule
    {
        /// <summary>
        /// Registers the password hasher, the token service and JwtBearer authentication
        /// configured from <see cref="TrussJwtOptions"/>.
        /// Options can be configured here or bound from the "Truss:Auth:Jwt" configuration section.
        /// Remember to call UseAuthentication and UseAuthorization in the pipeline.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of the JWT options.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussJwtAuth(
            this IServiceCollection services,
            Action<TrussJwtOptions>? configure = null)
        {
            services.AddOptions<TrussJwtOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.TryAddSingleton<IJwtTokenService, JwtTokenService>();

            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            services.AddAuthorization();

            return services;
        }
    }
}
