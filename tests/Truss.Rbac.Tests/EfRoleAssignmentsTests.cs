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

        [Fact]
        public async Task Assign_Roles_AndRevoke()
        {
            var store = new EfRoleAssignments<RbacDbContext>(_context);
            var userId = Guid.NewGuid();

            await store.Assign(userId, "admin");
            await store.Assign(userId, "support");
            await store.Assign(userId, "admin");

            Assert.Equal(["admin", "support"], (await store.RolesOf(userId)).Order());

            await store.Revoke(userId, "admin");

            Assert.Equal(["support"], await store.RolesOf(userId));
            Assert.Empty(await store.RolesOf(Guid.NewGuid()));
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
