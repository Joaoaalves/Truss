using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Truss.Observability.Tests.Fakes;
using Xunit;

namespace Truss.Observability.Tests
{
    public class CorrelationTests
    {
        private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<CorrelationRecorder>();
            builder.Services.AddTruss(options => options.AddAssembly<PingCommand>());
            builder.Services.AddTrussObservability();

            var app = builder.Build();
            app.UseTrussCorrelation();
            app.MapCommand<RecordCorrelationCommand>("/record");

            await app.StartAsync();
            return (app, app.GetTestClient());
        }

        [Fact]
        public async Task IncomingHeader_FlowsToHandler_AndEchoesBack()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var correlationId = Guid.NewGuid();
            var request = new HttpRequestMessage(HttpMethod.Post, "/record")
            {
                Content = JsonContent.Create(new RecordCorrelationCommand())
            };
            request.Headers.Add("X-Correlation-Id", correlationId.ToString());

            var response = await client.SendAsync(request);

            Assert.Equal(correlationId.ToString(), response.Headers.GetValues("X-Correlation-Id").Single());

            var recorder = app.Services.GetRequiredService<CorrelationRecorder>();
            var observed = Assert.Single(recorder.Observed);
            Assert.Equal(correlationId, observed);
        }

        [Fact]
        public async Task MissingHeader_GeneratesCorrelation_AndReturnsIt()
        {
            var (app, client) = await StartAppAsync();
            await using var _ = app;

            var response = await client.PostAsJsonAsync("/record", new RecordCorrelationCommand());

            var returned = Guid.Parse(response.Headers.GetValues("X-Correlation-Id").Single());
            Assert.NotEqual(Guid.Empty, returned);

            var recorder = app.Services.GetRequiredService<CorrelationRecorder>();
            var observed = Assert.Single(recorder.Observed);
            Assert.Equal(returned, observed);
        }
    }
}
