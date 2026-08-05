using Truss.Application;

namespace Truss.Application.Tests.Fakes
{
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
