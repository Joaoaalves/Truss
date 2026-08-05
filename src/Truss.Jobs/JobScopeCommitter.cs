using Microsoft.Extensions.DependencyInjection;
using Truss.Application;

namespace Truss.Jobs
{
    internal static class JobScopeCommitter
    {
        public static async Task Commit(IServiceProvider scopeProvider, CancellationToken cancellationToken)
        {
            var unitOfWork = scopeProvider.GetService<IUnitOfWork>();

            if (unitOfWork is not null)
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return;
            }

            await scopeProvider.GetRequiredService<IJobStore>().Save(cancellationToken);
        }
    }
}
