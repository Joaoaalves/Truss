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
        DateTimeOffset LastMessageOn);

    public sealed record SupportTicketMessage(
        Guid Id,
        SupportMessageAuthor Author,
        string Body,
        DateTimeOffset SentOn);

    public sealed record SupportTicket(
        Guid Id,
        string Subject,
        SupportTicketStatus Status,
        SupportTicketPriority Priority,
        Guid? LinkedFromTicketId,
        DateTimeOffset OpenedOn,
        IReadOnlyList<SupportTicketMessage> Messages);
}
