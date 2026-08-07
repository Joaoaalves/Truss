using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Tenancy;
using Truss.Tenancy.EntityFrameworkCore;
using Xunit;

namespace Truss.Tenancy.Tests
{
    public sealed class TenantDatabaseTests : IDisposable
    {
        private sealed class MappedConnections(Dictionary<Guid, string> map) : ITenantConnectionStrings
        {
            public string? ConnectionStringFor(Guid tenantId) => map.GetValueOrDefault(tenantId);
        }

        private static readonly Guid TenantA = Guid.NewGuid();
        private static readonly Guid TenantB = Guid.NewGuid();

        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"truss-tenantdb-{Guid.NewGuid():N}");
        private readonly ServiceProvider _provider;

        public TenantDatabaseTests()
        {
            Directory.CreateDirectory(_directory);

            var services = new ServiceCollection();
            services.AddSingleton<ITenantConnectionStrings>(new MappedConnections(new Dictionary<Guid, string>
            {
                [TenantA] = $"Data Source={Path.Combine(_directory, "tenant-a.db")}",
                [TenantB] = $"Data Source={Path.Combine(_directory, "tenant-b.db")}"
            }));
            services.AddDbContext<TenantDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(_directory, "default.db")}"));
            services.AddTrussTenancy<TenantDbContext>();

            _provider = services.BuildServiceProvider();

            foreach (var tenant in new Guid?[] { TenantA, TenantB, null })
            {
                TenantContextHolder.Current = tenant;
                using var scope = _provider.CreateScope();
                scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.EnsureCreated();
            }
        }

        [Fact]
        public void EachTenant_WritesAndReads_ItsOwnDatabase()
        {
            TenantContextHolder.Current = TenantA;

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
                context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "only-in-a" });
                context.SaveChanges();
            }

            using (var scope = _provider.CreateScope())
            {
                TenantContextHolder.Current = TenantB;
                Assert.Equal(0, scope.ServiceProvider.GetRequiredService<TenantDbContext>().Projects.IgnoreQueryFilters().Count());
            }

            using (var scope = _provider.CreateScope())
            {
                TenantContextHolder.Current = TenantA;
                Assert.Equal(1, scope.ServiceProvider.GetRequiredService<TenantDbContext>().Projects.Count());
            }

            Assert.True(File.Exists(Path.Combine(_directory, "tenant-a.db")));
            Assert.True(File.Exists(Path.Combine(_directory, "tenant-b.db")));
        }

        public void Dispose()
        {
            TenantContextHolder.Current = null;
            _provider.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
