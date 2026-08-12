using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Truss.Application;
using Truss.Messaging.Tests.Fakes;
using Xunit;

namespace Truss.Messaging.Tests
{
    /// <summary>
    /// The operational endpoints expose the counters and the dead-letter
    /// retry, so an operator fixes the broker and empties the dead letters
    /// with one POST instead of touching the database.
    /// </summary>
    public class OutboxEndpointTests : IAsyncLifetime
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private WebApplication? _app;
        private HttpClient? _client;
        private FakeTransport _transport = null!;

        public async Task InitializeAsync()
        {
            _connection.Open();
            _transport = new FakeTransport();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<ReceivedEvents>();
            builder.Services.AddDbContext<MessagingDbContext>(options => options.UseSqlite(_connection));
            builder.Services.AddTruss(options => options.AddAssembly<CreateItemCommand>());
            builder.Services.AddTrussEntityFramework<MessagingDbContext>();
            builder.Services.AddTrussMessaging(options => options.AddAssembly<CreateItemCommand>());
            builder.Services.AddSingleton<IMessageTransport>(_transport);
            builder.Services.AddTrussOutbox<MessagingDbContext>(options =>
            {
                options.RetryBaseDelay = TimeSpan.FromMilliseconds(1);
                options.MaxAttempts = 1;
            });

            _app = builder.Build();
            _app.MapTrussOutbox();

            using (var scope = _app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.EnsureCreated();
            }

            await _app.StartAsync();
            _client = _app.GetTestClient();
        }

        [Fact]
        public async Task Retry_EmptiesTheDeadLetters_AndTheCountersShowIt()
        {
            using (var scope = _app!.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new CreateItemCommand(Guid.NewGuid()));
            }

            _transport.Fail = true;
            await _app.Services.GetRequiredService<OutboxProcessor>().ProcessPendingAsync();

            var degraded = await _client!.GetFromJsonAsync<OutboxStatistics>("/truss/outbox");
            Assert.Equal(1, degraded!.FailedCount);

            _transport.Fail = false;
            var response = await _client.PostAsync("/truss/outbox/retry", null);
            var result = await response.Content.ReadFromJsonAsync<OutboxRetryResult>();
            Assert.Equal(1, result!.Retried);

            await _app.Services.GetRequiredService<OutboxProcessor>().ProcessPendingAsync();

            var recovered = await _client.GetFromJsonAsync<OutboxStatistics>("/truss/outbox");
            Assert.Equal(0, recovered!.FailedCount);
            Assert.Equal(0, recovered.PendingCount);
            Assert.Single(_transport.Published);
        }

        public async Task DisposeAsync()
        {
            if (_app is not null)
                await _app.DisposeAsync();

            _connection.Dispose();
        }
    }
}
