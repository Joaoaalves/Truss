namespace Truss.Jobs
{
    /// <summary>
    /// The persisted state of a background job: identity, arguments, lifecycle, progress and outcome.
    /// </summary>
    public class JobRecord
    {
        private JobRecord()
        {
            Name = string.Empty;
            ArgsPayload = string.Empty;
        }

        private JobRecord(Guid id, string name, string argsPayload, DateTimeOffset createdOn, DateTimeOffset? scheduledFor)
        {
            Id = id;
            Name = name;
            ArgsPayload = argsPayload;
            CreatedOn = createdOn;
            ScheduledFor = scheduledFor;
            Status = scheduledFor is null ? JobStatus.Queued : JobStatus.Scheduled;
        }

        /// <summary>
        /// Creates a job queued for immediate execution.
        /// </summary>
        public static JobRecord CreateQueued(Guid id, string name, string argsPayload, DateTimeOffset now)
        {
            return new JobRecord(id, name, argsPayload, now, scheduledFor: null);
        }

        /// <summary>
        /// Creates a job scheduled for a specific moment.
        /// </summary>
        public static JobRecord CreateScheduled(Guid id, string name, string argsPayload, DateTimeOffset now, DateTimeOffset runAt)
        {
            return new JobRecord(id, name, argsPayload, now, runAt);
        }

        /// <summary>Gets the job identifier.</summary>
        public Guid Id { get; private set; }

        /// <summary>Gets the stable name of the job type.</summary>
        public string Name { get; private set; }

        /// <summary>Gets the JSON payload of the job arguments.</summary>
        public string ArgsPayload { get; private set; }

        /// <summary>Gets the lifecycle status.</summary>
        public JobStatus Status { get; private set; }

        /// <summary>Gets the number of executions started so far.</summary>
        public int Attempts { get; private set; }

        /// <summary>Gets the last reported progress percentage.</summary>
        public int ProgressPercent { get; private set; }

        /// <summary>Gets the last reported progress message.</summary>
        public string? ProgressMessage { get; private set; }

        /// <summary>Gets the error of the last failed attempt.</summary>
        public string? Error { get; private set; }

        /// <summary>Gets when the job was created.</summary>
        public DateTimeOffset CreatedOn { get; private set; }

        /// <summary>Gets when the job is scheduled to run, for scheduled jobs.</summary>
        public DateTimeOffset? ScheduledFor { get; private set; }

        /// <summary>Gets when the last attempt started.</summary>
        public DateTimeOffset? StartedOn { get; private set; }

        /// <summary>Gets when the job reached a terminal status.</summary>
        public DateTimeOffset? CompletedOn { get; private set; }

        /// <summary>Gets whether cancellation was requested for this job.</summary>
        public bool CancellationRequested { get; private set; }

        /// <summary>
        /// Moves a scheduled job into the queue.
        /// </summary>
        public void MarkQueued()
        {
            Status = JobStatus.Queued;
        }

        /// <summary>
        /// Starts an execution attempt.
        /// </summary>
        public void MarkRunning(DateTimeOffset now)
        {
            Status = JobStatus.Running;
            Attempts++;
            StartedOn = now;
        }

        /// <summary>
        /// Records reported progress.
        /// </summary>
        public void UpdateProgress(int percent, string? message)
        {
            ProgressPercent = percent;

            if (message is not null)
                ProgressMessage = message;
        }

        /// <summary>
        /// Completes the job successfully.
        /// </summary>
        public void MarkSucceeded(DateTimeOffset now)
        {
            Status = JobStatus.Succeeded;
            ProgressPercent = 100;
            CompletedOn = now;
            Error = null;
        }

        /// <summary>
        /// Records a failed attempt and requeues the job for another try.
        /// With a next attempt moment, the job goes back to the scheduled state and the
        /// scheduler re-enqueues it when the backoff elapses; without one it is queued directly.
        /// </summary>
        public void PrepareRetry(string error, DateTimeOffset? nextAttemptOn = null)
        {
            Error = error;

            if (nextAttemptOn is { } runAt)
            {
                Status = JobStatus.Scheduled;
                ScheduledFor = runAt;
            }
            else
            {
                Status = JobStatus.Queued;
            }
        }

        /// <summary>
        /// Flags the job for cancellation. The executor observes the flag.
        /// </summary>
        public void RequestCancellation()
        {
            CancellationRequested = true;
        }

        /// <summary>
        /// Cancels the job.
        /// </summary>
        public void MarkCancelled(DateTimeOffset now)
        {
            Status = JobStatus.Cancelled;
            CompletedOn = now;
        }

        /// <summary>
        /// Fails the job permanently.
        /// </summary>
        public void MarkFailed(string error, DateTimeOffset now)
        {
            Status = JobStatus.Failed;
            Error = error;
            CompletedOn = now;
        }

        /// <summary>
        /// Projects the record into its read-only snapshot.
        /// </summary>
        public JobSnapshot ToSnapshot()
        {
            return new JobSnapshot(
                Id, Name, Status, Attempts, ProgressPercent, ProgressMessage, Error,
                CreatedOn, ScheduledFor, StartedOn, CompletedOn);
        }
    }
}
