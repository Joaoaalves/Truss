using Truss.Application.Abstractions.Events;
using Truss.Domain.Events;

namespace Truss.Persistence.EntityFrameworkCore.Tests.Fakes
{
    public class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Dispatched { get; } = [];

        public Func<IDomainEvent, Task>? OnDispatch { get; set; }

        public async Task Dispatch(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            foreach (var domainEvent in domainEvents)
            {
                Dispatched.Add(domainEvent);

                if (OnDispatch is not null)
                    await OnDispatch(domainEvent);
            }
        }
    }
}
