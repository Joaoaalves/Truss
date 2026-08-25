using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Truss.Application;
using Truss.EntityFrameworkCore.Tests.Fakes;
using Xunit;

namespace Truss.EntityFrameworkCore.Tests
{
    public class ToPageAsyncTests : IAsyncLifetime
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private TestDbContext _context = null!;

        public async Task InitializeAsync()
        {
            await _connection.OpenAsync();

            var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).Options;
            _context = new TestDbContext(options);
            await _context.Database.EnsureCreatedAsync();

            for (var i = 0; i < 5; i++)
                _context.Orders.Add(new Order(Guid.NewGuid()));

            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task ToPageAsync_ReturnsThePage_AndTheTotals()
        {
            var page = await _context.Orders
                .OrderBy(order => order.Id)
                .Select(order => order.Id)
                .ToPageAsync(new PageRequest(2, 2));

            Assert.Equal(2, page.Items.Count);
            Assert.Equal(2, page.Page);
            Assert.Equal(5, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.True(page.HasNextPage);
            Assert.True(page.HasPreviousPage);
        }

        [Fact]
        public async Task ToPageAsync_PastTheEnd_ReturnsAnEmptyPage()
        {
            var page = await _context.Orders
                .OrderBy(order => order.Id)
                .ToPageAsync(new PageRequest(4, 2));

            Assert.Empty(page.Items);
            Assert.Equal(5, page.TotalCount);
            Assert.False(page.HasNextPage);
        }

        public async Task DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
