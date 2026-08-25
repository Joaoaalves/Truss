using Microsoft.EntityFrameworkCore;
using Truss.Application;
using Truss.Domain;

namespace Truss.EntityFrameworkCore
{
    /// <summary>
    /// Entity Framework Core implementation of the unit of work.
    /// Collects domain events from tracked entities, dispatches them inside the transactional
    /// boundary and persists all resulting changes atomically in a single save.
    /// </summary>
    /// <typeparam name="TDbContext">The database context that owns the tracked entities.</typeparam>
    public class EfUnitOfWork<TDbContext>(TDbContext context, IDomainEventDispatcher domainEventDispatcher) : IUnitOfWork
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;
        private readonly IDomainEventDispatcher _domainEventDispatcher = domainEventDispatcher;

        /// <inheritdoc />
        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            await DispatchDomainEventsAsync(cancellationToken);
            return await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Dispatches domain events in rounds until no new events are raised.
        /// Handlers may modify tracked entities and raise further events; every change
        /// is persisted together in the same save.
        /// </summary>
        private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var domainEvents = CollectDomainEvents();

                if (domainEvents.Count == 0)
                    break;

                await _domainEventDispatcher.Dispatch(domainEvents, cancellationToken);
            }
        }

        /// <summary>
        /// Collects and clears the pending domain events from all tracked entities.
        /// </summary>
        private List<IDomainEvent> CollectDomainEvents()
        {
            var domainEvents = new List<IDomainEvent>();

            foreach (var entry in _context.ChangeTracker.Entries<IHasDomainEvents>())
            {
                if (entry.Entity.DomainEvents.Count == 0)
                    continue;

                domainEvents.AddRange(entry.Entity.DomainEvents);
                entry.Entity.ClearDomainEvents();
            }

            return domainEvents;
        }
    }
}
