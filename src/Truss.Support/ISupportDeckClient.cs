using Truss.Application;

namespace Truss.Support
{
    /// <summary>
    /// The application's line to the deck. Every call carries the service
    /// credential; commands carry an idempotency key so a retry can never
    /// duplicate a ticket or a message. When the deck is unreachable the
    /// calls throw <see cref="SupportDeckException"/>, naming the operation
    /// and the address, and the application's support surface degrades
    /// without taking anything else down.
    /// </summary>
    public interface ISupportDeckClient
    {
        Task<Guid> OpenTicket(SupportRequester requester, string subject, string body, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replies to a ticket and returns the id of the ticket that took the
        /// reply: the same one normally, a new linked one when the deck no
        /// longer accepts replies there.
        /// </summary>
        Task<Guid> Reply(Guid ticketId, SupportRequester requester, string body, CancellationToken cancellationToken = default);

        Task<PageResult<SupportTicketSummary>> ListTickets(string externalUserId, int page = 1, int size = 20, CancellationToken cancellationToken = default);

        Task<SupportTicket?> GetTicket(Guid ticketId, string externalUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a file onto the ticket. The deck validates the content
        /// before storing a byte and may hold the file in Scanning when a
        /// malware gate is configured; the receipt says which.
        /// </summary>
        Task<SupportAttachmentReceipt> UploadAttachment(Guid ticketId, string externalUserId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file of the ticket, or null while it is not available:
        /// missing, still scanning and rejected all answer the same.
        /// </summary>
        Task<SupportDownload?> DownloadAttachment(Guid ticketId, Guid attachmentId, string externalUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the conversation read for the requester, which clears the
        /// unread badge in the summaries. Idempotent.
        /// </summary>
        Task MarkRead(Guid ticketId, string externalUserId, CancellationToken cancellationToken = default);
    }
}
