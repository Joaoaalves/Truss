using Microsoft.EntityFrameworkCore;

namespace Truss.EntityFrameworkCore.Tests.Fakes
{
    public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyTrussIdempotency();
        }
    }
}
