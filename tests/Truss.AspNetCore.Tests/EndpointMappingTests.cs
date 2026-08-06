using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Truss.Application;
using Truss.AspNetCore.Tests.Fakes;
using Xunit;

namespace Truss.AspNetCore.Tests
{
    public class EndpointMappingTests
    {
        private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(Action<WebApplication> map)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddTruss(options => options.AddAssembly<PingCommand>());

            var app = builder.Build();
            map(app);

            await app.StartAsync();
            return (app, app.GetTestClient());
        }

        [Fact]
        public async Task MapCommand_WithResult_ReturnsOk()
        {
            var (app, client) = await StartAppAsync(app => app.MapCommand<PingCommand, string>("/ping"));
            await using var _ = app;

            var response = await client.PostAsJsonAsync("/ping", new PingCommand("abc"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("pong:abc", await response.Content.ReadFromJsonAsync<string>());
        }

        [Fact]
        public async Task MapQuery_BindsPagingFromTheQueryString()
        {
            var (app, client) = await StartAppAsync(app => app.MapQuery<ListNumbersQuery, PageResult<int>>("/numbers"));
            await using var _ = app;

            var page = await client.GetFromJsonAsync<PageResult<int>>("/numbers?page=2&size=2");

            Assert.NotNull(page);
            Assert.Equal([3, 4], page.Items);
            Assert.Equal(2, page.Page);
            Assert.Equal(5, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.True(page.HasNextPage);
        }

        [Fact]
        public async Task UseTrussIdempotency_ReadsTheHeaderIntoTheAmbientHolder()
        {
            var (app, client) = await StartAppAsync(app =>
            {
                app.UseTrussIdempotency();
                app.MapGet("/key", () => IdempotencyKeyHolder.Current ?? "none");
            });
            await using var _ = app;

            var request = new HttpRequestMessage(HttpMethod.Get, "/key");
            request.Headers.Add("Idempotency-Key", "abc-123");
            var response = await client.SendAsync(request);

            Assert.Equal("abc-123", await response.Content.ReadAsStringAsync());
            Assert.Equal("none", await client.GetStringAsync("/key"));
        }

        [Fact]
        public async Task MapCommand_WithoutResult_ReturnsNoContent()
        {
            var (app, client) = await StartAppAsync(app => app.MapCommand<ArchiveCommand>("/archive"));
            await using var _ = app;

            var response = await client.PostAsJsonAsync("/archive", new ArchiveCommand());

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task MapCommand_WithLocation_ReturnsCreated()
        {
            var (app, client) = await StartAppAsync(app =>
                app.MapCommand<CreateItemCommand, Guid>("/items", id => $"/items/{id}"));
            await using var _ = app;

            var response = await client.PostAsJsonAsync("/items", new CreateItemCommand("Beam"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var id = await response.Content.ReadFromJsonAsync<Guid>();
            Assert.Equal($"/items/{id}", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task MapCommand_InvalidRequest_ReturnsValidationProblemWithAllErrors()
        {
            var (app, client) = await StartAppAsync(app => app.MapCommand<PingCommand, string>("/ping"));
            await using var _ = app;

            var response = await client.PostAsJsonAsync("/ping", new PingCommand(""));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
            Assert.Equal(2, problem!.Errors["Value"].Length);
        }

        [Fact]
        public async Task MapCommand_BrokenBusinessRule_ReturnsUnprocessableEntity()
        {
            var (app, client) = await StartAppAsync(app => app.MapCommand<BreakRuleCommand>("/break"));
            await using var _ = app;

            var response = await client.PostAsJsonAsync("/break", new BreakRuleCommand());

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("A business rule was violated.", problem.GetProperty("title").GetString());
            Assert.Equal("The item is locked.", problem.GetProperty("detail").GetString());
            Assert.Equal(nameof(AlwaysBrokenRule), problem.GetProperty("rule").GetString());
            Assert.Equal("catalog.item-locked", problem.GetProperty("code").GetString());
        }

        [Fact]
        public async Task MapQuery_BindsFromQueryString()
        {
            var (app, client) = await StartAppAsync(app => app.MapQuery<GetGreetingQuery, string>("/greet"));
            await using var _ = app;

            var response = await client.GetAsync("/greet?name=Joao");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello Joao", await response.Content.ReadFromJsonAsync<string>());
        }

        [Fact]
        public async Task MapQuery_BindsFromRouteValues()
        {
            var (app, client) = await StartAppAsync(app => app.MapQuery<GetItemQuery, Guid>("/items/{id}"));
            await using var _ = app;

            var id = Guid.NewGuid();
            var response = await client.GetAsync($"/items/{id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(id, await response.Content.ReadFromJsonAsync<Guid>());
        }
    }
}
