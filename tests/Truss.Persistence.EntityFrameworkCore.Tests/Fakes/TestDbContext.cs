using Microsoft.EntityFrameworkCore;

namespace Truss.Persistence.EntityFrameworkCore.Tests.Fakes
{
    public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
    }
}
