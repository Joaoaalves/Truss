using Truss.Jobs.Storage;

namespace Truss.Jobs
{
    internal sealed class JobMonitor(IJobStore store) : IJobMonitor
    {
        public async Task<JobSnapshot?> Get(Guid jobId, CancellationToken cancellationToken = default)
        {
            var record = await store.Get(jobId, cancellationToken);
            return record?.ToSnapshot();
        }
    }
}
