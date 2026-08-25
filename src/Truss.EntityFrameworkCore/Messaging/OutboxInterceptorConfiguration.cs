using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Truss.Messaging;

namespace Truss.EntityFrameworkCore.Messaging
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
