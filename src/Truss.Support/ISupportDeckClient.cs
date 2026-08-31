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
    }
}
