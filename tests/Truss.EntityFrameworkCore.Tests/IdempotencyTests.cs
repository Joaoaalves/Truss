using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Truss.Application;
using Truss.EntityFrameworkCore.Tests.Fakes;
using Xunit;

namespace Truss.EntityFrameworkCore.Tests
{
    public sealed record RegisterSale(string Item) : ICommand<Guid>;

    public class SaleExecutions
    {
        private int _count;

        public int Count => _count;

        public void Increment() => Interlocked.Increment(ref _count);
    }

    public class RegisterSaleHandler(TestDbContext context, SaleExecutions executions) : ICommandHandler<RegisterSale, Guid>
    {
        public Task<Guid> Handle(RegisterSale command, CancellationToken cancellationToken)
        {
            executions.Increment();

            if (command.Item == "boom")
                throw new InvalidOperationException("failed sale");

            var order = new Order(Guid.NewGuid());
            context.Orders.Add(order);

            return Task.FromResult(order.Id);
        }
    }

    public class IdempotencyTests : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public IdempotencyTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<SaleExecutions>();
            services.AddDbContext<TestDbContext>(options => options.UseSqlite(_connection));
            services.AddTruss(options => options.AddAssembly<RegisterSale>());
            services.AddTrussEntityFramework<TestDbContext>();
            services.AddTrussIdempotency<TestDbContext>();

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
        }

        private async Task<Guid> Send(RegisterSale command, string? key)
        {
            IdempotencyKeyHolder.Current = key;

            try
            {
                using var scope = _provider.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(command);
            }
            finally
            {
                IdempotencyKeyHolder.Current = null;
            }
        }

        private SaleExecutions Executions => _provider.GetRequiredService<SaleExecutions>();

        [Fact]
        public async Task SameKey_ExecutesOnce_AndReplaysTheResponse()
        {
            var first = await Send(new RegisterSale("beam"), "key-1");
            var replay = await Send(new RegisterSale("beam"), "key-1");

            Assert.Equal(first, replay);
            Assert.Equal(1, Executions.Count);

            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Assert.Equal(1, await context.Orders.CountAsync());
        }

        [Fact]
        public async Task DifferentKeys_ExecuteIndependently()
        {
            var first = await Send(new RegisterSale("beam"), "key-1");
            var second = await Send(new RegisterSale("beam"), "key-2");

            Assert.NotEqual(first, second);
            Assert.Equal(2, Executions.Count);
        }

        [Fact]
        public async Task WithoutKey_EveryCallExecutes()
        {
            await Send(new RegisterSale("beam"), null);
            await Send(new RegisterSale("beam"), null);

            Assert.Equal(2, Executions.Count);
        }

        [Fact]
        public async Task FailedCommand_StoresNothing_SoTheRetryRuns()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Send(new RegisterSale("boom"), "key-1"));

            var recovered = await Send(new RegisterSale("beam"), "key-1");

            Assert.NotEqual(Guid.Empty, recovered);
            Assert.Equal(2, Executions.Count);
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
