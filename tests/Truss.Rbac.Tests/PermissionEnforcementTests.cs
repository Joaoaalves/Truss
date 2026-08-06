using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Truss.Rbac;
using Xunit;

namespace Truss.Rbac.Tests
{
    internal sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Claims", out var header))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = header.ToString()
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('='))
                .Select(parts => new Claim(parts[0], parts[1]));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    public sealed class FakeAssignments : IRoleAssignments
    {
        public Dictionary<Guid, List<string>> Roles { get; } = [];

        public Task<IReadOnlyList<string>> RolesOf(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(Roles.GetValueOrDefault(userId, []));
        }

        public Task Assign(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            Roles.TryAdd(userId, []);
            Roles[userId].Add(role);
            return Task.CompletedTask;
        }

        public Task Revoke(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            Roles.GetValueOrDefault(userId)?.Remove(role);
            return Task.CompletedTask;
        }
    }

    public class PermissionEnforcementTests
    {
        private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(FakeAssignments? assignments = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            builder.Services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

            builder.Services.AddTrussRbac(options =>
            {
                options.AddRole("admin", "catalog.write", "orders.refund");
                options.AddRole("support", "orders.read");
            });

            if (assignments is not null)
                builder.Services.AddSingleton<IRoleAssignments>(assignments);

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/write", () => "ok").RequirePermission("catalog.write");

            await app.StartAsync();
            return (app, app.GetTestClient());
        }

        private static HttpRequestMessage Request(string claims)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/write");
            request.Headers.Add("X-Test-Claims", claims);
            return request;
        }

        [Fact]
        public async Task RoleWithThePermission_IsAllowed()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var response = await client.SendAsync(Request("role=admin"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task RoleWithoutThePermission_IsForbidden()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var response = await client.SendAsync(Request("role=support"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AnonymousCaller_IsChallenged()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var response = await client.GetAsync("/write");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task StoredRoles_AreResolvedFromTheSubClaim()
        {
            var userId = Guid.NewGuid();
            var assignments = new FakeAssignments();
            await assignments.Assign(userId, "admin");

            var (app, client) = await StartAppAsync(assignments);
            await using var _ = app;

            var response = await client.SendAsync(Request($"sub={userId}"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UserWithoutStoredRoles_StaysForbidden()
        {
            var (app, client) = await StartAppAsync(new FakeAssignments());
            await using var _ = app;

            var response = await client.SendAsync(Request($"sub={Guid.NewGuid()}"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
