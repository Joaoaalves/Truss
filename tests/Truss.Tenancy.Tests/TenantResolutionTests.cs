using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Truss.Tenancy;
using Xunit;

namespace Truss.Tenancy.Tests
{
    public class TenantResolutionTests
    {
        private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(Action<TrussTenancyOptions>? configure = null, Guid? claimTenant = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            var app = builder.Build();

            if (claimTenant is { } tenant)
            {
                app.Use((context, next) =>
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant", tenant.ToString())], "test"));
                    return next(context);
                });
            }

            app.UseTrussTenancy(configure);
            app.MapGet("/tenant", () => TenantContextHolder.Current?.ToString() ?? "none");

            await app.StartAsync();
            return (app, app.GetTestClient());
        }

        [Fact]
        public async Task Header_ResolvesTheTenant()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var tenant = Guid.NewGuid();
            var request = new HttpRequestMessage(HttpMethod.Get, "/tenant");
            request.Headers.Add("X-Tenant-Id", tenant.ToString());

            var response = await client.SendAsync(request);

            Assert.Equal(tenant.ToString(), await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Claim_WinsOverTheHeader()
        {
            var claimTenant = Guid.NewGuid();
            var (app, client) = await StartAppAsync(claimTenant: claimTenant);
            await using var _ = app;

            var request = new HttpRequestMessage(HttpMethod.Get, "/tenant");
            request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

            var response = await client.SendAsync(request);

            Assert.Equal(claimTenant.ToString(), await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task CustomResolver_ReplacesTheDefaults()
        {
            var tenant = Guid.NewGuid();
            var (app, client) = await StartAppAsync(options => options.Resolver = context =>
                context.Request.Query.TryGetValue("t", out var value) && Guid.TryParse(value.ToString(), out var parsed)
                    ? parsed
                    : null);
            await using var _ = app;

            Assert.Equal(tenant.ToString(), await client.GetStringAsync($"/tenant?t={tenant}"));
            Assert.Equal("none", await client.GetStringAsync("/tenant"));
        }

        [Fact]
        public async Task MissingTenant_FlowsAsNone()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            Assert.Equal("none", await client.GetStringAsync("/tenant"));
        }
    }
}
