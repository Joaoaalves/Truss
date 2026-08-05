using System.ComponentModel;
using Truss.Messaging;

namespace Truss.Jobs
{
    /// <summary>
    /// Infrastructure event that triggers the execution of a queued job.
    /// Published through the messaging pipeline so job delivery inherits the outbox
    /// transactionality and the transport's durability. Not intended for application code.
    /// </summary>
    /// <param name="JobId">The identifier of the job to execute.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [IntegrationEventName("truss.jobs.enqueued")]
    public sealed record JobEnqueued(Guid JobId) : IntegrationEvent;
}
