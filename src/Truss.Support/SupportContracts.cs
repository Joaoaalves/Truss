using System.Text.Json.Serialization;

namespace Truss.Support
{
    /// <summary>
    /// The person behind a ticket, as the application describes them: its own
    /// user id plus a display snapshot the deck refreshes on every message.
    /// </summary>
    public sealed record SupportRequester(string ExternalUserId, string Email, string DisplayName);

    /// <summary>
    /// The ticket's life on the deck. Open and WaitingOnCustomer alternate
    /// with the conversation; Resolved accepts a reply within the deck's
    /// reopen window; Closed is terminal, and a late reply opens a new
    /// ticket linked to the old one.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SupportTicketStatus>))]
    public enum SupportTicketStatus
    {
        Open,
        WaitingOnCustomer,
        Resolved,
        Closed
    }

    [JsonConverter(typeof(JsonStringEnumConverter<SupportTicketPriority>))]
    public enum SupportTicketPriority
    {
        Normal,
        High,
        Urgent
    }

    [JsonConverter(typeof(JsonStringEnumConverter<SupportMessageAuthor>))]
    public enum SupportMessageAuthor
    {
        Customer,
        Agent
    }

    public sealed record SupportTicketSummary(
        Guid Id,
        string Subject,
        SupportTicketStatus Status,
        SupportTicketPriority Priority,
        DateTimeOffset OpenedOn,
        DateTimeOffset LastMessageOn,
        bool Unread = false);

    public sealed record SupportTicketMessage(
        Guid Id,
        SupportMessageAuthor Author,
        string Body,
        DateTimeOffset SentOn);

    /// <summary>
    /// The life of a file on the deck: Scanning while the malware gate holds
    /// it, Available when it downloads, Rejected when the scan said no and
    /// the bytes are gone.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SupportAttachmentStatus>))]
    public enum SupportAttachmentStatus
    {
        Scanning,
        Available,
        Rejected
    }

    public sealed record SupportAttachment(
        Guid Id,
        SupportMessageAuthor Author,
        string FileName,
        string ContentType,
        long SizeBytes,
        SupportAttachmentStatus Status,
        DateTimeOffset UploadedOn);

    public sealed record SupportTicket(
        Guid Id,
        string Subject,
        SupportTicketStatus Status,
        SupportTicketPriority Priority,
        Guid? LinkedFromTicketId,
        DateTimeOffset OpenedOn,
        IReadOnlyList<SupportTicketMessage> Messages,
        IReadOnlyList<SupportAttachment>? Attachments = null);

    /// <summary>What an upload answers: the record's id and whether a scan holds it.</summary>
    public sealed record SupportAttachmentReceipt(Guid AttachmentId, SupportAttachmentStatus Status);

    /// <summary>A downloaded file: the caller owns the stream.</summary>
    public sealed record SupportDownload(Stream Content, string ContentType, string FileName) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return Content.DisposeAsync();
        }
    }
}
