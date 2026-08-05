using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Truss.Messaging;

namespace Truss.Jobs
{
    internal sealed class JobEnqueuedHandler(
        IJobStore store,
        JobTypeRegistry registry,
        IIntegrationEventPublisher publisher,
        IServiceProvider provider,
        IOptions<TrussJobsOptions> options,
        ILogger<JobEnqueuedHandler> logger,
        TimeProvider timeProvider) : IIntegrationEventHandler<JobEnqueued>
    {
        private readonly TrussJobsOptions _options = options.Value;

        public async Task Handle(JobEnqueued integrationEvent, CancellationToken cancellationToken)
        {
            var record = await store.Get(integrationEvent.JobId, cancellationToken);

            if (record is null)
            {
                logger.LogWarning("Job {JobId} was triggered but no record exists; ignoring.", integrationEvent.JobId);
                return;
            }

            if (record.Status is JobStatus.Succeeded or JobStatus.Failed)
                return;

            var descriptor = registry.Resolve(record.Name);

            if (descriptor is null)
            {
                record.MarkFailed($"No job type is registered for name '{record.Name}'.", timeProvider.GetUtcNow());
                await store.Save(cancellationToken);
                logger.LogError("Job {JobId} references unknown job name '{Name}'.", record.Id, record.Name);
                return;
            }

            record.MarkRunning(timeProvider.GetUtcNow());
            await store.Save(cancellationToken);

            var context = new JobContext(record.Id, record.Attempts, async (percent, message, progressToken) =>
            {
                record.UpdateProgress(percent, message);
                await store.Save(progressToken);
            });

            try
            {
                await ExecuteWithTimeout(descriptor, record, context, cancellationToken);

                record.MarkSucceeded(timeProvider.GetUtcNow());
                await store.Save(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                record.PrepareRetry("Execution was interrupted by shutdown.");
                await store.Save(CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                await RegisterFailure(record, exception, cancellationToken);
            }
        }

        private async Task ExecuteWithTimeout(JobDescriptor descriptor, JobRecord record, JobContext context, CancellationToken cancellationToken)
        {
            if (_options.JobTimeout is not { } timeout)
            {
                await descriptor.Invoker.Invoke(provider, record.ArgsPayload, context, cancellationToken);
                return;
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                await descriptor.Invoker.Invoke(provider, record.ArgsPayload, context, timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Job execution exceeded the {timeout} limit.");
            }
        }

        private async Task RegisterFailure(JobRecord record, Exception exception, CancellationToken cancellationToken)
        {
            if (record.Attempts >= _options.MaxAttempts)
            {
                record.MarkFailed(exception.Message, timeProvider.GetUtcNow());
                await store.Save(cancellationToken);

                logger.LogError(exception, "Job {JobId} ({Name}) failed permanently after {Attempts} attempts.", record.Id, record.Name, record.Attempts);
                return;
            }

            record.PrepareRetry(exception.Message);
            await store.Save(cancellationToken);
            await publisher.Publish(new JobEnqueued(record.Id), cancellationToken);
            await store.Save(cancellationToken);

            logger.LogWarning(exception, "Job {JobId} ({Name}) failed attempt {Attempts}; requeued.", record.Id, record.Name, record.Attempts);
        }
    }
}
