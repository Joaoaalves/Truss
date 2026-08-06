using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Truss.Application;
using Truss.Jobs;
using Truss.Messaging;

namespace Truss.Testing.Tests.Fakes
{
    public class Order
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyTrussOutbox();
            modelBuilder.ApplyTrussJobs();
        }
    }

    [IntegrationEventName("testing.order-placed")]
    public sealed record OrderPlaced(Guid OrderId) : IntegrationEvent;

    public class ReceivedEvents
    {
        public ConcurrentQueue<OrderPlaced> Events { get; } = new();
    }

    public class OrderPlacedHandler(ReceivedEvents received) : IIntegrationEventHandler<OrderPlaced>
    {
        public Task Handle(OrderPlaced integrationEvent, CancellationToken cancellationToken)
        {
            received.Events.Enqueue(integrationEvent);
            return Task.CompletedTask;
        }
    }

    public sealed record PlaceOrder(string Name) : ICommand<Guid>;

    public class PlaceOrderValidator : AbstractValidator<PlaceOrder>
    {
        public PlaceOrderValidator()
        {
            RuleFor(command => command.Name).NotEmpty();
        }
    }

    public class PlaceOrderHandler(OrdersDbContext context, IIntegrationEventPublisher publisher)
        : ICommandHandler<PlaceOrder, Guid>
    {
        public async Task<Guid> Handle(PlaceOrder command, CancellationToken cancellationToken)
        {
            var order = new Order { Id = Guid.NewGuid(), Name = command.Name };
            context.Orders.Add(order);

            await publisher.Publish(new OrderPlaced(order.Id), cancellationToken);

            return order.Id;
        }
    }

    public sealed record ExportArgs(string Target);

    [JobName("testing.export")]
    public class ExportJob : IJob<ExportArgs>
    {
        public async Task Execute(ExportArgs args, JobContext context, CancellationToken cancellationToken)
        {
            await context.ReportProgress(50, $"Exporting {args.Target}", cancellationToken);
            await context.ReportProgress(100, "Done", cancellationToken);
        }
    }

    public sealed record StartExport(string Target) : ICommand<Guid>;

    public class StartExportHandler(IJobScheduler scheduler) : ICommandHandler<StartExport, Guid>
    {
        public Task<Guid> Handle(StartExport command, CancellationToken cancellationToken)
        {
            return scheduler.Enqueue<ExportJob, ExportArgs>(new ExportArgs(command.Target), cancellationToken);
        }
    }

    public sealed record Ping(string Message) : ICommand<string>;

    public class PingHandler : ICommandHandler<Ping, string>
    {
        public Task<string> Handle(Ping command, CancellationToken cancellationToken)
        {
            return Task.FromResult($"pong: {command.Message}");
        }
    }
}
