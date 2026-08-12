using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Truss.Application;
using Truss.Domain;
using Xunit;

namespace Truss.Remote.Tests
{
    public sealed record StockDto(Guid ProductId, int Available);

    public sealed record GetStock(Guid ProductId) : IQuery<StockDto?>;

    public class GetStockHandler : IQueryHandler<GetStock, StockDto?>
    {
        public static readonly Guid Known = Guid.Parse("6b29fc40-ca47-1067-b31d-00dd010662da");

        public Task<StockDto?> Handle(GetStock request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.ProductId == Known ? new StockDto(request.ProductId, 42) : (StockDto?)null);
        }
    }

    public class GetStockValidator : AbstractValidator<GetStock>
    {
        public GetStockValidator()
        {
            RuleFor(query => query.ProductId).NotEmpty();
        }
    }

    public sealed record CountReserved(Guid ProductId) : IQuery<int>;

    public sealed record ReserveStock(Guid ProductId) : ICommand;

    public class ReserveStockHandler : ICommandHandler<ReserveStock>
    {
        public Task<Unit> Handle(ReserveStock request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Unit.Value);
        }
    }

    public sealed class WarehouseClosed : IBusinessRule
    {
        public bool IsBroken() => true;

        public string Message => "The warehouse is closed.";

        public string Code => "inventory.warehouse-closed";
    }

    public class CountReservedHandler : IQueryHandler<CountReserved, int>
    {
        public Task<int> Handle(CountReserved request, CancellationToken cancellationToken)
        {
            throw new BusinessRuleValidationException(new WarehouseClosed());
        }
    }

    /// <summary>
    /// AddRemoteContext gives a caller the same outcomes a local dispatch
    /// would: the result, null for not found, the validation failure and the
    /// business rule with its stable code, all across the wire.
    /// </summary>
    public class RemoteContextTests : IAsyncLifetime
    {
        private WebApplication? _server;
        private ServiceProvider? _client;

        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddTruss(options => options.AddAssembly<GetStock>());

            _server = builder.Build();
            _server.MapRemoteContext(typeof(GetStock).Assembly);
            await _server.StartAsync();

            var services = new ServiceCollection();
            services.AddLogging();

            // The client side has no local handlers; the abstractions assembly
            // satisfies the pipeline and every answer must cross the wire.
            services.AddTruss(options => options.AddAssembly<ValidationError>());
            services.AddRemoteContext<GetStock>("Inventory", new Uri("http://localhost"));
            services.AddHttpClient("truss-remote-Inventory")
                .ConfigurePrimaryHttpMessageHandler(() => _server.GetTestServer().CreateHandler());

            _client = services.BuildServiceProvider();
        }

        private IDispatcher Dispatcher()
        {
            return _client!.CreateScope().ServiceProvider.GetRequiredService<IDispatcher>();
        }

        [Fact]
        public async Task AQuery_CrossesTheWire_AndAnswersLikeALocalOne()
        {
            var stock = await Dispatcher().Send(new GetStock(GetStockHandler.Known));

            Assert.NotNull(stock);
            Assert.Equal(42, stock.Available);
        }

        [Fact]
        public async Task ANullResult_StaysNull()
        {
            Assert.Null(await Dispatcher().Send(new GetStock(Guid.Parse("11111111-1111-1111-1111-111111111111"))));
        }

        [Fact]
        public async Task TheRemoteValidation_SurfacesAsARequestValidationException()
        {
            var exception = await Assert.ThrowsAsync<RequestValidationException>(
                () => Dispatcher().Send(new GetStock(Guid.Empty)));

            Assert.Contains(exception.Errors, error => error.PropertyName == "ProductId");
        }

        [Fact]
        public async Task TheRemoteRule_SurfacesWithItsStableCode()
        {
            var exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
                () => Dispatcher().Send(new CountReserved(Guid.NewGuid())));

            Assert.Equal("inventory.warehouse-closed", exception.BrokenRule.Code);
        }

        [Fact]
        public async Task AnUnreachableContext_FailsAsARemoteContextException()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTruss(options => options.AddAssembly<ValidationError>());
            services.AddRemoteContext<GetStock>("Nowhere", new Uri("http://localhost:59999"), options => options.Timeout = TimeSpan.FromMilliseconds(500));

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            await Assert.ThrowsAsync<RemoteContextException>(
                () => scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new GetStock(Guid.NewGuid())));
        }

        [Fact]
        public void Commands_AreDeliberatelyNotWired()
        {
            var wired = Microsoft.Extensions.DependencyInjection.TrussRemoteModule.Queries(typeof(GetStock).Assembly).Select(entry => entry.Query);

            Assert.Contains(typeof(GetStock), wired);
            Assert.DoesNotContain(typeof(ReserveStock), wired);
        }

        public async Task DisposeAsync()
        {
            if (_client is not null)
                await _client.DisposeAsync();

            if (_server is not null)
                await _server.DisposeAsync();
        }
    }
}
