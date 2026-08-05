using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Truss.Auth;
using Xunit;

namespace Truss.Auth.Jwt.Tests
{
    public class JwtAuthIntegrationTests
    {
        private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddTrussJwtAuth(options =>
            {
                options.Issuer = "truss-tests";
                options.Audience = "truss-tests";
                options.SigningKey = new string('k', 48);
            });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/secure", (ClaimsPrincipal user) => user.FindFirstValue("email")).RequireAuthorization();

            await app.StartAsync();
            return (app, app.GetTestClient());
        }

        [Fact]
        public async Task SecureEndpoint_WithoutToken_Returns401()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var response = await client.GetAsync("/secure");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task SecureEndpoint_WithIssuedToken_ReturnsClaims()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var tokens = app.Services.GetRequiredService<IJwtTokenService>();
            var token = tokens.CreateAccessToken([new Claim("email", "joao@example.com")]);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/secure");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("joao@example.com", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task SecureEndpoint_WithTokenFromDifferentKey_Returns401()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var foreign = new JwtTokenService(
                Microsoft.Extensions.Options.Options.Create(new TrussJwtOptions
                {
                    Issuer = "truss-tests",
                    Audience = "truss-tests",
                    SigningKey = new string('x', 48)
                }),
                TimeProvider.System);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", foreign.CreateAccessToken([new Claim("email", "joao@example.com")]));
            var response = await client.GetAsync("/secure");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
