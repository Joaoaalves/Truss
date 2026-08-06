using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Tenancy;
using Xunit;

namespace Truss.Tenancy.Tests
{
    public class Project
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class Setting
    {
        public Guid Id { get; set; }

        public string Key { get; set; } = string.Empty;
    }

    public class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
    {
        public DbSet<Project> Projects => Set<Project>();

        public DbSet<Setting> Settings => Set<Setting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>().IsTenantOwned();
            modelBuilder.ApplyTrussTenancy(this);
        }
    }

    public sealed class TenantIsolationTests : IDisposable
    {
        private static readonly Guid TenantA = Guid.NewGuid();
        private static readonly Guid TenantB = Guid.NewGuid();

        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public TenantIsolationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<TenantDbContext>(options => options.UseSqlite(_connection));
            services.AddTrussTenancy<TenantDbContext>();

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.EnsureCreated();
        }

        private TenantDbContext Context(IServiceScope scope)
        {
            return scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        }

        [Fact]
        public void TenantOwnedRows_AreStamped_AndInvisibleToOtherTenants()
        {
            using (var scope = _provider.CreateScope())
            {
                TenantContextHolder.Current = TenantA;
                var context = Context(scope);
                context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "a1" });
                context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "a2" });
                context.SaveChanges();

                TenantContextHolder.Current = TenantB;
                context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "b1" });
                context.SaveChanges();
            }

            using (var scope = _provider.CreateScope())
            {
                var context = Context(scope);

                TenantContextHolder.Current = TenantA;
                Assert.Equal(2, context.Projects.Count());

                TenantContextHolder.Current = TenantB;
                Assert.Equal(1, context.Projects.Count());
                Assert.Equal("b1", context.Projects.Single().Name);

                TenantContextHolder.Current = null;
                Assert.Equal(0, context.Projects.Count());

                Assert.Equal(3, context.Projects.IgnoreQueryFilters().Count());
            }
        }

        [Fact]
        public void SavingTenantOwnedData_WithoutATenant_FailsLoudly()
        {
            using var scope = _provider.CreateScope();
            TenantContextHolder.Current = null;

            var context = Context(scope);
            context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "orphan" });

            var exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
            Assert.Contains("tenant-owned", exception.Message);
        }

        [Fact]
        public void UnmarkedEntities_AreUntouched()
        {
            using (var scope = _provider.CreateScope())
            {
                TenantContextHolder.Current = null;
                var context = Context(scope);
                context.Settings.Add(new Setting { Id = Guid.NewGuid(), Key = "shared" });
                context.SaveChanges();
            }

            using (var scope = _provider.CreateScope())
            {
                TenantContextHolder.Current = TenantA;
                Assert.Equal(1, Context(scope).Settings.Count());

                TenantContextHolder.Current = null;
                Assert.Equal(1, Context(scope).Settings.Count());
            }
        }

        public void Dispose()
        {
            TenantContextHolder.Current = null;
            _provider.Dispose();
            _connection.Dispose();
        }
    }
}
