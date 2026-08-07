using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Truss.Rbac.EntityFrameworkCore;
using Xunit;

namespace Truss.Rbac.Tests
{
    public class RbacDbContext(DbContextOptions<RbacDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyTrussRbac();
        }
    }

    public sealed class EfRoleAssignmentsTests : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly RbacDbContext _context;

        public EfRoleAssignmentsTests()
        {
            _connection.Open();
            _context = new RbacDbContext(new DbContextOptionsBuilder<RbacDbContext>().UseSqlite(_connection).Options);
            _context.Database.EnsureCreated();
        }

        private sealed class FixedScope(Guid? scopeId) : Truss.Rbac.IRoleScope
        {
            public Guid? CurrentScopeId => scopeId;
        }

        [Fact]
        public async Task Assign_Roles_AndRevoke()
        {
            var store = new EfRoleAssignments<RbacDbContext>(_context, new FixedScope(null));
            var userId = Guid.NewGuid();

            await store.Assign(userId, "admin");
            await store.Assign(userId, "support");
            await store.Assign(userId, "admin");

            Assert.Equal(["admin", "support"], (await store.RolesOf(userId)).Order());

            await store.Revoke(userId, "admin");

            Assert.Equal(["support"], await store.RolesOf(userId));
            Assert.Empty(await store.RolesOf(Guid.NewGuid()));
        }

        [Fact]
        public async Task TenantScopedGrants_OnlyApplyInsideTheirTenant()
        {
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var writer = new EfRoleAssignments<RbacDbContext>(_context, new FixedScope(null));
            await writer.Assign(userId, "support");
            await writer.Assign(userId, "admin", tenantA);

            var inTenantA = new EfRoleAssignments<RbacDbContext>(_context, new FixedScope(tenantA));
            Assert.Equal(["admin", "support"], (await inTenantA.RolesOf(userId)).Order());

            var inTenantB = new EfRoleAssignments<RbacDbContext>(_context, new FixedScope(tenantB));
            Assert.Equal(["support"], await inTenantB.RolesOf(userId));

            var global = new EfRoleAssignments<RbacDbContext>(_context, new FixedScope(null));
            Assert.Equal(["support"], await global.RolesOf(userId));
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
