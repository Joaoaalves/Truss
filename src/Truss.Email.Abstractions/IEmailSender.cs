namespace Truss.Email
{
    /// <summary>
    /// Sends email. Handlers depend on this abstraction; the host decides the
    /// mechanism. Sending from an integration event handler or a job inherits
    /// that runtime's retry, which is the recommended shape for anything that
    /// must not be lost.
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>
        /// Sends one message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Send(EmailMessage message, CancellationToken cancellationToken = default);
    }
}
