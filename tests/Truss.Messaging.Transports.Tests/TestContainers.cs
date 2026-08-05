using System.Diagnostics;
using Npgsql;
using StackExchange.Redis;
using Xunit;

namespace Truss.Messaging.Transports.Tests
{
    public static class TestContainers
    {
        public const string PostgresAdminConnectionString =
            "Host=localhost;Port=54329;Username=postgres;Password=truss;Database=postgres";

        public const string PostgresConnectionString =
            "Host=localhost;Port=54329;Username=postgres;Password=truss;Database=truss_test";

        public const string RedisConnectionString = "localhost:63799";

        public static async Task<string> EnsurePostgres()
        {
            await WaitUntilReady("truss-test-postgres", "run -d --name truss-test-postgres -p 54329:5432 -e POSTGRES_PASSWORD=truss postgres:16-alpine", async () =>
            {
                await using var connection = new NpgsqlConnection(PostgresAdminConnectionString);
                await connection.OpenAsync();
            });

            await using (var connection = new NpgsqlConnection(PostgresAdminConnectionString))
            {
                await connection.OpenAsync();
                await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = 'truss_test'", connection);

                if (await exists.ExecuteScalarAsync() is null)
                {
                    await using var create = new NpgsqlCommand("CREATE DATABASE truss_test", connection);
                    await create.ExecuteNonQueryAsync();
                }
            }

            return PostgresConnectionString;
        }

        public static async Task<string> EnsureRedis()
        {
            await WaitUntilReady("truss-test-redis", "run -d --name truss-test-redis -p 63799:6379 redis:7-alpine", async () =>
            {
                var connection = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString + ",abortConnect=false,connectTimeout=2000");
                await connection.GetDatabase().PingAsync();
                await connection.DisposeAsync();
            });

            return RedisConnectionString;
        }

        private static async Task WaitUntilReady(string containerName, string runArguments, Func<Task> probe)
        {
            var deadline = DateTime.UtcNow.AddSeconds(120);
            var started = false;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await probe();
                    return;
                }
                catch
                {
                    if (!started)
                    {
                        RunDocker($"start {containerName}");
                        RunDocker(runArguments);
                        started = true;
                    }

                    await Task.Delay(1000);
                }
            }

            Assert.Fail($"Container {containerName} did not become ready. Is docker running?");
        }

        private static void RunDocker(string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("docker", arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                process?.WaitForExit(30_000);
            }
            catch
            {
            }
        }
    }

    public class PostgresFixture : IAsyncLifetime
    {
        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            ConnectionString = await TestContainers.EnsurePostgres();
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    public class RedisFixture : IAsyncLifetime
    {
        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            ConnectionString = await TestContainers.EnsureRedis();
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    [CollectionDefinition("postgres")]
    public class PostgresCollection : ICollectionFixture<PostgresFixture>
    {
    }

    [CollectionDefinition("redis")]
    public class RedisCollection : ICollectionFixture<RedisFixture>
    {
    }
}
