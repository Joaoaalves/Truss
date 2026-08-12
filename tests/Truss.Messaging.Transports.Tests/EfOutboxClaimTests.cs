using Microsoft.EntityFrameworkCore;
using Truss.Messaging.EntityFrameworkCore;
using Xunit;

namespace Truss.Messaging.Transports.Tests
{
    /// <summary>
    /// Two processor instances share one Postgres outbox. A fetch must claim its
    /// rows until the store saves, so concurrent batches are disjoint and no
    /// message is published twice just because two instances woke up together.
    /// </summary>
    public class EfOutboxClaimTests
    {
        private sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : DbContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.ApplyTrussOutbox();
            }
        }

        private static OutboxDbContext CreateContext(string connectionString)
        {
            var options = new DbContextOptionsBuilder<OutboxDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new OutboxDbContext(options);
        }

        [Fact]
        public async Task ConcurrentFetches_ClaimDisjointBatches()
        {
            var connectionString = await TestContainers.EnsurePostgres();
            var now = DateTimeOffset.UtcNow;

            await using (var setup = CreateContext(connectionString))
            {
                await setup.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"TrussOutbox\"");
                await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());

                for (var index = 0; index < 4; index++)
                    setup.Add(new OutboxMessage(Guid.NewGuid(), "test.claimed", 1, "{}", now.AddSeconds(index)));

                await setup.SaveChangesAsync();
            }

            await using var first = CreateContext(connectionString);
            await using var second = CreateContext(connectionString);
            var firstStore = new EfOutboxStore<OutboxDbContext>(first);
            var secondStore = new EfOutboxStore<OutboxDbContext>(second);

            var firstBatch = await firstStore.FetchDue(2, now.AddMinutes(1));
            var secondBatch = await secondStore.FetchDue(10, now.AddMinutes(1));

            Assert.Equal(2, firstBatch.Count);
            Assert.Equal(2, secondBatch.Count);
            Assert.Empty(firstBatch.Select(message => message.Id).Intersect(secondBatch.Select(message => message.Id)));

            foreach (var message in firstBatch.Concat(secondBatch))
                message.MarkProcessed(now.AddMinutes(1));

            await firstStore.Save();
            await secondStore.Save();

            await using var check = CreateContext(connectionString);
            var checkStore = new EfOutboxStore<OutboxDbContext>(check);

            Assert.Empty(await checkStore.FetchDue(10, now.AddMinutes(2)));

            var statistics = await checkStore.GetStatistics();
            Assert.Equal(0, statistics.PendingCount);
        }
    }
}
