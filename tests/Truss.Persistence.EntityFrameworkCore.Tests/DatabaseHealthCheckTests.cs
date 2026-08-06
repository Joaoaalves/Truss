using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Truss.Persistence.EntityFrameworkCore;
using Truss.Persistence.EntityFrameworkCore.Tests.Fakes;
using Xunit;

namespace Truss.Persistence.EntityFrameworkCore.Tests
{
    public class DatabaseHealthCheckTests
    {
        [Fact]
        public async Task ReachableDatabase_IsHealthy()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddDbContext<TestDbContext>(options => options.UseSqlite(connection));
            services.AddHealthChecks().AddTrussDatabase<TestDbContext>();

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var check = new DatabaseHealthCheck<TestDbContext>(scope.ServiceProvider.GetRequiredService<TestDbContext>());
            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task UnreachableDatabase_IsUnhealthy()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), "truss-missing", Guid.NewGuid().ToString("N"), "no.db")}")
                .Options;

            await using var context = new TestDbContext(options);

            var check = new DatabaseHealthCheck<TestDbContext>(context);
            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }
    }
}
