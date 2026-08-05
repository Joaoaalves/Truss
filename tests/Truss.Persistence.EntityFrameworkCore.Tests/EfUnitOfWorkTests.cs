using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Truss.Persistence.EntityFrameworkCore.Tests.Fakes;
using Xunit;

namespace Truss.Persistence.EntityFrameworkCore.Tests
{
    public class EfUnitOfWorkTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestDbContext _context;
        private readonly RecordingDomainEventDispatcher _dispatcher;
        private readonly EfUnitOfWork<TestDbContext> _unitOfWork;

        public EfUnitOfWorkTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new TestDbContext(options);
            _context.Database.EnsureCreated();

            _dispatcher = new RecordingDomainEventDispatcher();
            _unitOfWork = new EfUnitOfWork<TestDbContext>(_context, _dispatcher);
        }

        [Fact]
        public async Task CommitAsync_PersistsChanges()
        {
            var order = new Order(Guid.NewGuid());
            order.Place();
            _context.Orders.Add(order);

            await _unitOfWork.CommitAsync();

            Assert.Equal(1, await _context.Orders.CountAsync());
        }

        [Fact]
        public async Task CommitAsync_DispatchesAndClearsDomainEvents()
        {
            var order = new Order(Guid.NewGuid());
            order.Place();
            _context.Orders.Add(order);

            await _unitOfWork.CommitAsync();

            var dispatched = Assert.Single(_dispatcher.Dispatched);
            var placed = Assert.IsType<OrderPlaced>(dispatched);
            Assert.Equal(order.Id, placed.OrderId);
            Assert.Empty(order.DomainEvents);
        }

        [Fact]
        public async Task CommitAsync_DispatchesEventsRaisedByHandlers()
        {
            var order = new Order(Guid.NewGuid());
            order.Place();
            _context.Orders.Add(order);

            _dispatcher.OnDispatch = domainEvent =>
            {
                if (domainEvent is OrderPlaced)
                    order.Archive();

                return Task.CompletedTask;
            };

            await _unitOfWork.CommitAsync();

            Assert.Equal(2, _dispatcher.Dispatched.Count);
            Assert.IsType<OrderPlaced>(_dispatcher.Dispatched[0]);
            Assert.IsType<OrderArchived>(_dispatcher.Dispatched[1]);

            var persisted = await _context.Orders.SingleAsync(o => o.Id == order.Id);
            Assert.Equal("Archived", persisted.Status);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
