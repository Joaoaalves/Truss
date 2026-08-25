using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Truss.Messaging;
using Truss.Jobs.Storage;

namespace Truss.Jobs.Runtime
{
    internal sealed class JobEnqueuedHandler(
        IJobStore store,
        JobTypeRegistry registry,
        IIntegrationEventPublisher publisher,
        IServiceProvider provider,
        IServiceScopeFactory scopeFactory,
        IOptions<TrussJobsOptions> options,
        ILogger<JobEnqueuedHandler> logger,
        TimeProvider timeProvider,
        JobMetrics metrics) : IIntegrationEventHandler<JobEnqueued>
    {
        private static readonly ActivitySource Source = new("Truss.Jobs");

        private readonly TrussJobsOptions _options = options.Value;

        public async Task Handle(JobEnqueued integrationEvent, CancellationToken cancellationToken)
        {
            var record = await store.Get(integrationEvent.JobId, cancellationToken);

            if (record is null)
            {
                logger.LogWarning("Job {JobId} was triggered but no record exists; ignoring.", integrationEvent.JobId);
                return;
            }

            if (record.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled)
                return;

            if (record.CancellationRequested)
            {
                record.MarkCancelled(timeProvider.GetUtcNow());
                await store.Save(cancellationToken);
                return;
            }

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

            using var activity = Source.StartActivity($"job {record.Name}");
            activity?.SetTag("truss.job", record.Name);
            activity?.SetTag("truss.job_id", record.Id);
            activity?.SetTag("truss.job.attempt", record.Attempts);

            var context = new JobContext(record.Id, record.Attempts, async (percent, message, progressToken) =>
            {
                record.UpdateProgress(percent, message);
                await store.Save(progressToken);
            });

            using var executionSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var watcherSource = new CancellationTokenSource();
            var watcher = WatchForCancellation(record.Id, executionSource, watcherSource.Token);
            var startedAt = timeProvider.GetTimestamp();

            try
            {
                await ExecuteWithTimeout(descriptor, record, context, executionSource, cancellationToken);

                record.MarkSucceeded(timeProvider.GetUtcNow());
                await store.Save(cancellationToken);

                metrics.Executed("succeeded", record.Name, timeProvider.GetElapsedTime(startedAt));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                record.PrepareRetry("Execution was interrupted by shutdown.");
                await store.Save(CancellationToken.None);
                throw;
            }
            catch (JobCancelledException)
            {
                record.MarkCancelled(timeProvider.GetUtcNow());
                await store.Save(cancellationToken);

                metrics.Executed("cancelled", record.Name, timeProvider.GetElapsedTime(startedAt));
                logger.LogInformation("Job {JobId} ({Name}) was cancelled during attempt {Attempts}.", record.Id, record.Name, record.Attempts);
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                await RegisterFailure(record, exception, cancellationToken);
                metrics.Executed(record.Status == JobStatus.Failed ? "failed" : "retried", record.Name, timeProvider.GetElapsedTime(startedAt));
            }
            finally
            {
                watcherSource.Cancel();
                await watcher;
            }
        }

        private async Task ExecuteWithTimeout(
            JobDescriptor descriptor,
            JobRecord record,
            JobContext context,
            CancellationTokenSource executionSource,
            CancellationToken hostToken)
        {
            if (_options.JobTimeout is { } timeout)
                executionSource.CancelAfter(timeout);

            try
            {
                await descriptor.Invoker.Invoke(provider, record.ArgsPayload, context, executionSource.Token);
            }
            catch (OperationCanceledException) when (!hostToken.IsCancellationRequested)
            {
                if (record.CancellationRequested)
                    throw new JobCancelledException();

                throw new TimeoutException($"Job execution exceeded the {_options.JobTimeout} limit.");
            }
        }

        /// <summary>
        /// Polls the store from its own scope and cancels the execution token when a
        /// cancellation request appears, so running jobs observe it cooperatively.
        /// </summary>
        private async Task WatchForCancellation(Guid jobId, CancellationTokenSource executionSource, CancellationToken watcherToken)
        {
            try
            {
                while (!watcherToken.IsCancellationRequested)
                {
                    await Task.Delay(_options.CancellationPollingInterval, watcherToken);

                    await using var scope = scopeFactory.CreateAsyncScope();
                    var freshStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
                    var fresh = await freshStore.Get(jobId, watcherToken);

                    if (fresh?.CancellationRequested == true)
                    {
                        executionSource.Cancel();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The cancellation watcher of job {JobId} stopped.", jobId);
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

            if (RetryDelayFor(record.Attempts) is { } delay)
            {
                record.PrepareRetry(exception.Message, timeProvider.GetUtcNow() + delay);
                await store.Save(cancellationToken);

                logger.LogWarning(exception, "Job {JobId} ({Name}) failed attempt {Attempts}; next attempt at {NextAttemptOn}.", record.Id, record.Name, record.Attempts, record.ScheduledFor);
                return;
            }

            record.PrepareRetry(exception.Message);
            await store.Save(cancellationToken);
            await publisher.Publish(new JobEnqueued(record.Id), cancellationToken);
            await store.Save(cancellationToken);

            logger.LogWarning(exception, "Job {JobId} ({Name}) failed attempt {Attempts}; requeued.", record.Id, record.Name, record.Attempts);
        }

        /// <summary>
        /// Computes the exponential backoff before the next attempt, or null when
        /// the base delay is zero and the retry should be immediate.
        /// </summary>
        private TimeSpan? RetryDelayFor(int attemptsSoFar)
        {
            if (_options.RetryBaseDelay <= TimeSpan.Zero)
                return null;

            var delay = _options.RetryBaseDelay * Math.Pow(2, attemptsSoFar - 1);
            return delay > _options.RetryMaxDelay ? _options.RetryMaxDelay : delay;
        }
    }
}
