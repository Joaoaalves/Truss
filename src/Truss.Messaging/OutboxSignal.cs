namespace Truss.Messaging
{
    /// <summary>
    /// In-process wake-up signal for the outbox processor.
    /// Stores that publish outbox messages notify it after their transaction commits,
    /// so delivery starts immediately instead of waiting for the next polling interval.
    /// Polling remains as the safety net for retries and for messages written by other instances.
    /// </summary>
    public sealed class OutboxSignal
    {
        private readonly SemaphoreSlim _signal = new(0, 1);

        /// <summary>
        /// Wakes the outbox processor. Safe to call from any thread; extra notifications coalesce.
        /// </summary>
        public void Notify()
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        }

        /// <summary>
        /// Waits until notified or until the timeout elapses.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><c>true</c> when woken by a notification; <c>false</c> on timeout.</returns>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _signal.WaitAsync(timeout, cancellationToken);
        }
    }
}
