using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Truss.Jobs.Tests.Fakes;
using Xunit;

namespace Truss.Jobs.Tests
{
    public class JobEndpointsTests
    {
        private static async Task<(WebApplication App, HttpClient Client, SqliteConnection Connection)> StartAppAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddDbContext<JobsDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddTruss(options => options.AddAssembly<StartReportCommand>());
            builder.Services.AddTrussEntityFramework<JobsDbContext>();
            builder.Services.AddTrussMessaging(options => options.AddAssembly<StartReportCommand>());
            builder.Services.AddTrussInMemoryTransport();
            builder.Services.AddTrussOutbox<JobsDbContext>(options => options.PollingInterval = TimeSpan.FromMilliseconds(50));
            builder.Services.AddTrussJobs(options => options.AddAssembly<ReportJob>());
            builder.Services.AddTrussJobsEntityFramework<JobsDbContext>();

            var app = builder.Build();
            app.MapTrussJobs(streamInterval: TimeSpan.FromMilliseconds(100));

            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<JobsDbContext>().Database.EnsureCreated();
            }

            await app.StartAsync();
            return (app, app.GetTestClient(), connection);
        }

        [Fact]
        public async Task GetJob_ReturnsSnapshot()
        {
            var (app, client, connection) = await StartAppAsync();
            await using var _ = app;
            using var __ = connection;

            Guid jobId;

            using (var scope = app.Services.CreateScope())
            {
                var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
                jobId = await scheduler.Enqueue<ReportJob, ReportArgs>(new ReportArgs("api"));
                await scope.ServiceProvider.GetRequiredService<Truss.Application.IUnitOfWork>().CommitAsync();
            }

            var response = await client.GetAsync($"/truss/jobs/{jobId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var snapshot = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(jobId, snapshot.GetProperty("id").GetGuid());
            Assert.Equal("test.report", snapshot.GetProperty("name").GetString());
            Assert.Equal("queued", snapshot.GetProperty("status").GetString());
        }

        [Fact]
        public async Task GetUnknownJob_ReturnsNotFound()
        {
            var (app, client, connection) = await StartAppAsync();
            await using var _ = app;
            using var __ = connection;

            var response = await client.GetAsync($"/truss/jobs/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task StreamJob_PushesEventsUntilCompletion()
        {
            var (app, client, connection) = await StartAppAsync();
            await using var _ = app;
            using var __ = connection;

            Guid jobId;

            using (var scope = app.Services.CreateScope())
            {
                var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
                jobId = await scheduler.Enqueue<ReportJob, ReportArgs>(new ReportArgs("stream"));
                await scope.ServiceProvider.GetRequiredService<Truss.Application.IUnitOfWork>().CommitAsync();
            }

            using var response = await client.GetAsync(
                $"/truss/jobs/{jobId}/stream", HttpCompletionOption.ResponseHeadersRead);

            Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);

            var body = await response.Content.ReadAsStringAsync();

            Assert.Contains("data: ", body);
            Assert.Contains("\"succeeded\"", body.ToLowerInvariant());
        }
    }
}
