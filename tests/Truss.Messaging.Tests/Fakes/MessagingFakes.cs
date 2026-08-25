using Microsoft.EntityFrameworkCore;
using Truss.Application;
using Truss.Messaging;
using Truss.Messaging.Serialization;
using Truss.Messaging.Transport;

namespace Truss.Messaging.Tests.Fakes
{
    [IntegrationEventName("test.item-created", Version = 1)]
    public sealed record ItemCreated(Guid ItemId) : IntegrationEvent;

    [IntegrationEventName("test.item-created", Version = 2)]
    public sealed record ItemCreatedV2(Guid ItemId, string Name) : IntegrationEvent;

    public sealed record UnnamedEvent(string Value) : IntegrationEvent;

    [IntegrationEventName("test.throwing")]
    public sealed record ThrowingEvent : IntegrationEvent;

    public class ReceivedEvents
    {
        private readonly Lock _lock = new();
        private readonly List<IIntegrationEvent> _events = [];

        public void Add(IIntegrationEvent integrationEvent)
        {
            lock (_lock)
            {
                _events.Add(integrationEvent);
            }
        }

        public IReadOnlyList<IIntegrationEvent> Snapshot()
        {
            lock (_lock)
            {
                return [.. _events];
            }
        }
    }

    public class ItemCreatedHandler(ReceivedEvents received) : IIntegrationEventHandler<ItemCreated>
    {
        public Task Handle(ItemCreated integrationEvent, CancellationToken cancellationToken)
        {
            received.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    public class ThrowingEventHandler : IIntegrationEventHandler<ThrowingEvent>
    {
        public Task Handle(ThrowingEvent integrationEvent, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Handler failed.");
        }
    }

    public sealed record CreateItemCommand(Guid ItemId) : ICommand;

    public class CreateItemCommandHandler(IIntegrationEventPublisher publisher) : ICommandHandler<CreateItemCommand>
    {
        public async Task<Unit> Handle(CreateItemCommand request, CancellationToken cancellationToken)
        {
            await publisher.Publish(new ItemCreated(request.ItemId), cancellationToken);
            return Unit.Value;
        }
    }

    public sealed record FailingCommand : ICommand;

    public class FailingCommandHandler(IIntegrationEventPublisher publisher) : ICommandHandler<FailingCommand>
    {
        public async Task<Unit> Handle(FailingCommand request, CancellationToken cancellationToken)
        {
            await publisher.Publish(new ItemCreated(Guid.NewGuid()), cancellationToken);
            throw new InvalidOperationException("Handler failed.");
        }
    }

    public sealed record FlakyEvent(Guid Id) : IntegrationEvent;

    /// <summary>
    /// Fails its first handling and succeeds afterwards, so a test can prove
    /// the inbox record rolls back with the failed attempt and only sticks
    /// with a successful one.
    /// </summary>
    public class FlakyEventHandler(ReceivedEvents received) : IIntegrationEventHandler<FlakyEvent>
    {
        private static int _attempts;

        public static void Reset() => Interlocked.Exchange(ref _attempts, 0);

        public Task Handle(FlakyEvent integrationEvent, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attempts) == 1)
                throw new InvalidOperationException("First delivery fails.");

            received.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    public class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyTrussOutbox();
            modelBuilder.ApplyTrussInbox();
        }
    }

    public class FakeTransport : IMessageTransport
    {
        private readonly Lock _lock = new();
        private readonly List<IntegrationEventEnvelope> _published = [];

        public bool Fail { get; set; }

        public IReadOnlyList<IntegrationEventEnvelope> Published
        {
            get
            {
                lock (_lock)
                {
                    return [.. _published];
                }
            }
        }

        public Task Publish(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("Transport is down.");

            lock (_lock)
            {
                _published.Add(envelope);
            }

            return Task.CompletedTask;
        }
    }

    public class FakeUnitOfWork : IUnitOfWork
    {
        public int Commits { get; private set; }

        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            Commits++;
            return Task.FromResult(0);
        }
    }
}
