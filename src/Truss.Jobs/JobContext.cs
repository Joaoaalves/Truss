namespace Truss.Jobs
{
    /// <summary>
    /// Execution context handed to a running job.
    /// Progress reports are persisted immediately, so they are visible to
    /// polling and streaming consumers while the job is still running.
    /// </summary>
    public sealed class JobContext(
        Guid jobId,
        int attempt,
        Func<int, string?, CancellationToken, Task> reportProgress)
    {
        /// <summary>
        /// Gets the identifier of the job being executed.
        /// </summary>
        public Guid JobId { get; } = jobId;

        /// <summary>
        /// Gets the current attempt number, starting at 1.
        /// </summary>
        public int Attempt { get; } = attempt;

        /// <summary>
        /// Reports execution progress.
        /// </summary>
        /// <param name="percent">The completed percentage, from 0 to 100.</param>
        /// <param name="message">An optional human-readable progress message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public Task ReportProgress(int percent, string? message = null, CancellationToken cancellationToken = default)
        {
            return reportProgress(Math.Clamp(percent, 0, 100), message, cancellationToken);
        }
    }
}
