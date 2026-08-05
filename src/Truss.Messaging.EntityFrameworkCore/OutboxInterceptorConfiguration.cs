using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Truss.Messaging.EntityFrameworkCore
{
    internal sealed class OutboxInterceptorConfiguration<TDbContext>(OutboxCommitInterceptor interceptor)
        : IDbContextOptionsConfiguration<TDbContext>
        where TDbContext : DbContext
    {
        public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
    }
}
