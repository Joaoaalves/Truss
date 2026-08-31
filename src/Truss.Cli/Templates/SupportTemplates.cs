namespace Truss.Cli.Templates
{
    /// <summary>
    /// The support context the CLI scaffolds: a Ticket aggregate with the
    /// documented state machine, customer and staff slices, persistence and
    /// tests. Like the account slice, it is the user's code once written.
    /// Tokens beyond the usual __NAME__: __USERID__ and __NS_USERID__ resolve
    /// the account's id type, so a merge-bound account keeps working.
    /// </summary>
    internal static class SupportTemplates
    {
        public const string SupportPolicy = """
            namespace __NAME__.Domain.Support
            {
                /// <summary>
                /// The support preferences of this application. These are yours to
                /// edit; the state machine on the Ticket treats them as input, so
                /// changing a value here never breaks an invariant.
                /// </summary>
                public static class SupportPolicy
                {
                    /// <summary>
                    /// How long a resolved ticket accepts a customer reply before it
                    /// closes for good. A reply after the window opens a new ticket
                    /// linked to the old one.
                    /// </summary>
                    public static readonly TimeSpan ReopenWindow = TimeSpan.FromDays(7);
                }
            }
            """;

        public const string TicketStatus = """
            namespace __NAME__.Domain.Support.Ticket
            {
                /// <summary>
                /// The life of a ticket. Open and WaitingOnCustomer alternate with
                /// the conversation; Resolved accepts a customer reply within the
                /// reopen window; Closed is terminal, and a reply after it opens a
                /// new ticket linked to this one.
                /// </summary>
                public enum TicketStatus
                {
                    Open,
                    WaitingOnCustomer,
                    Resolved,
                    Closed
                }
            }
            """;

        public const string TicketPriority = """
            namespace __NAME__.Domain.Support.Ticket
            {
                /// <summary>
                /// Triage set by staff, never by the requester: a priority the
                /// customer chooses stops meaning anything.
                /// </summary>
                public enum TicketPriority
                {
                    Normal,
                    High,
                    Urgent
                }
            }
            """;

        public const string MessageVisibility = """
            namespace __NAME__.Domain.Support.Ticket
            {
                /// <summary>
                /// Public messages belong to the conversation; Internal ones are
                /// notes between staff and never leave the staff surface.
                /// </summary>
                public enum MessageVisibility
                {
                    Public,
                    Internal
                }
            }
            """;

        public const string MessageAuthorKind = """
            namespace __NAME__.Domain.Support.Ticket
            {
                public enum MessageAuthorKind
                {
                    Customer,
                    Staff
                }
            }
            """;

        public const string TicketId = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.ValueObjects
            {
                public sealed record TicketId(Guid Value) : TypedId<Guid>(Value);
            }
            """;

        public const string TicketMessageId = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.ValueObjects
            {
                public sealed record TicketMessageId(Guid Value) : TypedId<Guid>(Value);
            }
            """;

        public const string TicketSubject = """
            using __NAME__.Domain.Support.Ticket.Rules;
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.ValueObjects
            {
                public sealed record TicketSubject
                {
                    public string Value { get; }

                    private TicketSubject(string value)
                    {
                        Value = value;
                    }

                    public static TicketSubject Create(string value)
                    {
                        var normalized = value?.Trim() ?? string.Empty;

                        BusinessRule.Check(new TicketSubjectMustFitLength(normalized));

                        return new TicketSubject(normalized);
                    }

                    public override string ToString() => Value;
                }
            }
            """;

        public const string MessageBody = """
            using __NAME__.Domain.Support.Ticket.Rules;
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.ValueObjects
            {
                /// <summary>
                /// Plain text, always. Message bodies render as text on every
                /// surface; treating them as markup would hand the requester a
                /// script injection into the staff screen.
                /// </summary>
                public sealed record MessageBody
                {
                    public string Value { get; }

                    private MessageBody(string value)
                    {
                        Value = value;
                    }

                    public static MessageBody Create(string value)
                    {
                        var normalized = value?.Trim() ?? string.Empty;

                        BusinessRule.Check(new MessageBodyMustFitLength(normalized));

                        return new MessageBody(normalized);
                    }

                    public override string ToString() => Value;
                }
            }
            """;

        public const string RuleSubjectLength = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.Rules
            {
                public class TicketSubjectMustFitLength(string subject) : IBusinessRule
                {
                    public bool IsBroken() => subject.Length is < 3 or > 200;

                    public string Message => "The subject must have between 3 and 200 characters.";

                    public string Code => "support.subject-length";
                }
            }
            """;

        public const string RuleBodyLength = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.Rules
            {
                public class MessageBodyMustFitLength(string body) : IBusinessRule
                {
                    public bool IsBroken() => body.Length is < 1 or > 10_000;

                    public string Message => "The message must have between 1 and 10000 characters.";

                    public string Code => "support.message-length";
                }
            }
            """;

        public const string RuleTicketMustExist = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.Rules
            {
                /// <summary>
                /// Covers both the missing ticket and the ticket of somebody else,
                /// on purpose: answering differently would confirm which ids exist.
                /// </summary>
                public class TicketMustExist(bool found) : IBusinessRule
                {
                    public bool IsBroken() => !found;

                    public string Message => "The ticket does not exist.";

                    public string Code => "support.ticket-missing";
                }
            }
            """;

        public const string RuleNotClosed = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.Rules
            {
                public class TicketMustNotBeClosed(TicketStatus status) : IBusinessRule
                {
                    public bool IsBroken() => status == TicketStatus.Closed;

                    public string Message => "The ticket is closed.";

                    public string Code => "support.ticket-closed";
                }
            }
            """;

        public const string RuleMustBeActive = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.Rules
            {
                public class TicketMustBeActive(TicketStatus status) : IBusinessRule
                {
                    public bool IsBroken() => status is not (TicketStatus.Open or TicketStatus.WaitingOnCustomer);

                    public string Message => "The ticket is not active.";

                    public string Code => "support.ticket-not-active";
                }
            }
            """;

        public const string RuleAcceptsCustomerReply = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.Rules
            {
                public class TicketMustAcceptCustomerReplies(bool accepts) : IBusinessRule
                {
                    public bool IsBroken() => !accepts;

                    public string Message => "The ticket no longer accepts replies; open a new one.";

                    public string Code => "support.closed-to-replies";
                }
            }
            """;

        public const string RuleResolvedTakesOnlyNotes = """
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.Rules
            {
                /// <summary>
                /// A public staff reply on a resolved ticket would reopen a closed
                /// conversation on the staff's initiative; notes stay internal.
                /// </summary>
                public class ResolvedTicketTakesOnlyInternalNotes(TicketStatus status) : IBusinessRule
                {
                    public bool IsBroken() => status == TicketStatus.Resolved;

                    public string Message => "A resolved ticket takes internal notes only.";

                    public string Code => "support.resolved-takes-only-notes";
                }
            }
            """;

        public const string Events = """
            using __NAME__.Domain.Support.Ticket.ValueObjects;
            using __NS_USERID__;
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket.Events
            {
                public sealed record TicketOpened(TicketId TicketId, __USERID__ RequesterId) : DomainEvent;

                public sealed record CustomerReplied(TicketId TicketId) : DomainEvent;

                public sealed record StaffReplied(TicketId TicketId, bool Internal) : DomainEvent;

                public sealed record TicketResolved(TicketId TicketId) : DomainEvent;

                public sealed record TicketReopened(TicketId TicketId) : DomainEvent;

                public sealed record TicketClosed(TicketId TicketId) : DomainEvent;
            }
            """;

        public const string TicketMessage = """
            using __NAME__.Domain.Support.Ticket.ValueObjects;
            using __NS_USERID__;
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket
            {
                /// <summary>
                /// One entry of the conversation. Messages are appended through the
                /// Ticket and never edited: support history is evidence.
                /// </summary>
                public class TicketMessage : Entity<TicketMessageId>
                {
                    private TicketMessage()
                    {
                    }

                    internal TicketMessage(
                        TicketMessageId id,
                        MessageAuthorKind authorKind,
                        __USERID__ authorId,
                        MessageVisibility visibility,
                        MessageBody body,
                        DateTimeOffset sentOn) : base(id)
                    {
                        AuthorKind = authorKind;
                        AuthorId = authorId;
                        Visibility = visibility;
                        Body = body;
                        SentOn = sentOn;
                    }

                    public MessageAuthorKind AuthorKind { get; private set; }

                    public __USERID__ AuthorId { get; private set; } = null!;

                    public MessageVisibility Visibility { get; private set; }

                    public MessageBody Body { get; private set; } = null!;

                    public DateTimeOffset SentOn { get; private set; }
                }
            }
            """;

        public const string Ticket = """
            using __NAME__.Domain.Support.Ticket.Events;
            using __NAME__.Domain.Support.Ticket.Rules;
            using __NAME__.Domain.Support.Ticket.ValueObjects;
            using __NS_USERID__;
            using Truss.Domain;

            namespace __NAME__.Domain.Support.Ticket
            {
                /// <summary>
                /// A support conversation. The state machine is the invariant:
                /// Open and WaitingOnCustomer alternate with the replies, Resolved
                /// reopens on a customer reply within SupportPolicy.ReopenWindow,
                /// and Closed is terminal. Timestamps come in from the caller so
                /// the domain never reads a clock.
                /// </summary>
                public class Ticket : AggregateRoot<TicketId>
                {
                    private readonly List<TicketMessage> _messages = [];

                    private Ticket()
                    {
                    }

                    private Ticket(TicketId id, __USERID__ requesterId, TicketSubject subject, DateTimeOffset openedOn, TicketId? linkedFromTicketId) : base(id)
                    {
                        RequesterId = requesterId;
                        Subject = subject;
                        Status = TicketStatus.Open;
                        Priority = TicketPriority.Normal;
                        OpenedOn = openedOn;
                        LastMessageOn = openedOn;
                        LinkedFromTicketId = linkedFromTicketId;
                    }

                    public __USERID__ RequesterId { get; private set; } = null!;

                    public TicketSubject Subject { get; private set; } = null!;

                    public TicketStatus Status { get; private set; }

                    public TicketPriority Priority { get; private set; }

                    /// <summary>
                    /// Gets the closed ticket this one continues, when the requester
                    /// replied after the conversation had closed for good.
                    /// </summary>
                    public TicketId? LinkedFromTicketId { get; private set; }

                    public DateTimeOffset OpenedOn { get; private set; }

                    public DateTimeOffset LastMessageOn { get; private set; }

                    public DateTimeOffset? ResolvedOn { get; private set; }

                    public DateTimeOffset? ClosedOn { get; private set; }

                    public IReadOnlyList<TicketMessage> Messages => _messages;

                    public static Ticket Open(__USERID__ requesterId, TicketSubject subject, MessageBody body, DateTimeOffset now, TicketId? linkedFromTicketId = null)
                    {
                        var ticket = new Ticket(new TicketId(Guid.NewGuid()), requesterId, subject, now, linkedFromTicketId);

                        ticket.Append(MessageAuthorKind.Customer, requesterId, MessageVisibility.Public, body, now);
                        ticket.AddDomainEvent(new TicketOpened(ticket.Id, requesterId));

                        return ticket;
                    }

                    /// <summary>
                    /// Whether a customer reply lands on this ticket. When it does
                    /// not, the caller opens a new ticket linked to this one; the
                    /// domain never creates another aggregate on its own.
                    /// </summary>
                    public bool AcceptsCustomerReply(DateTimeOffset now)
                    {
                        return Status is TicketStatus.Open or TicketStatus.WaitingOnCustomer
                            || (Status is TicketStatus.Resolved && now <= ResolvedOn!.Value + SupportPolicy.ReopenWindow);
                    }

                    public void CustomerReply(MessageBody body, DateTimeOffset now)
                    {
                        BusinessRule.Check(new TicketMustAcceptCustomerReplies(AcceptsCustomerReply(now)));

                        var reopened = Status == TicketStatus.Resolved;

                        Append(MessageAuthorKind.Customer, RequesterId, MessageVisibility.Public, body, now);
                        Status = TicketStatus.Open;
                        ResolvedOn = null;

                        AddDomainEvent(reopened ? new TicketReopened(Id) : new CustomerReplied(Id));
                    }

                    public void StaffReply(__USERID__ staffId, MessageBody body, MessageVisibility visibility, DateTimeOffset now)
                    {
                        BusinessRule.Check(new TicketMustNotBeClosed(Status));

                        if (visibility == MessageVisibility.Public)
                        {
                            BusinessRule.Check(new ResolvedTicketTakesOnlyInternalNotes(Status));

                            if (Status == TicketStatus.Open)
                                Status = TicketStatus.WaitingOnCustomer;
                        }

                        Append(MessageAuthorKind.Staff, staffId, visibility, body, now);
                        AddDomainEvent(new StaffReplied(Id, visibility == MessageVisibility.Internal));
                    }

                    public void Resolve(DateTimeOffset now)
                    {
                        BusinessRule.Check(new TicketMustBeActive(Status));

                        Status = TicketStatus.Resolved;
                        ResolvedOn = now;

                        AddDomainEvent(new TicketResolved(Id));
                    }

                    public void Close(DateTimeOffset now)
                    {
                        BusinessRule.Check(new TicketMustNotBeClosed(Status));

                        Status = TicketStatus.Closed;
                        ClosedOn = now;

                        AddDomainEvent(new TicketClosed(Id));
                    }

                    /// <summary>
                    /// The auto-close sweep: a resolved ticket whose reopen window
                    /// has passed closes for good. Returns whether it closed.
                    /// </summary>
                    public bool CloseIfExpired(DateTimeOffset now)
                    {
                        if (Status != TicketStatus.Resolved || now < ResolvedOn!.Value + SupportPolicy.ReopenWindow)
                            return false;

                        Close(now);
                        return true;
                    }

                    public void SetPriority(TicketPriority priority)
                    {
                        BusinessRule.Check(new TicketMustNotBeClosed(Status));

                        Priority = priority;
                    }

                    private void Append(MessageAuthorKind authorKind, __USERID__ authorId, MessageVisibility visibility, MessageBody body, DateTimeOffset now)
                    {
                        _messages.Add(new TicketMessage(new TicketMessageId(Guid.NewGuid()), authorKind, authorId, visibility, body, now));
                        LastMessageOn = now;
                    }
                }
            }
            """;

        public const string Repository = """
            using __NAME__.Domain.Support.Ticket;
            using __NAME__.Domain.Support.Ticket.ValueObjects;
            using __NAME__.Application.Support.DTOs;
            using __NS_USERID__;
            using Truss.Application;

            namespace __NAME__.Application.Support
            {
                public interface ITicketRepository
                {
                    void Add(Ticket ticket);

                    Task<Ticket?> GetById(TicketId id, CancellationToken cancellationToken = default);

                    Task<PageResult<TicketSummaryDto>> ListFor(__USERID__ requesterId, PageRequest page, CancellationToken cancellationToken = default);

                    /// <summary>
                    /// The full conversation. With a requester the ticket must belong
                    /// to them and internal notes are filtered out; without one it is
                    /// the staff view, notes included.
                    /// </summary>
                    Task<TicketDto?> GetDetail(TicketId id, __USERID__? requesterId, CancellationToken cancellationToken = default);

                    Task<PageResult<TicketSummaryDto>> Queue(TicketStatus? status, PageRequest page, CancellationToken cancellationToken = default);

                    Task<IReadOnlyList<Ticket>> ResolvedBefore(DateTimeOffset cutoff, int limit, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string Dtos = """
            using __NAME__.Domain.Support.Ticket;

            namespace __NAME__.Application.Support.DTOs
            {
                public sealed record TicketSummaryDto(
                    Guid Id,
                    string Subject,
                    TicketStatus Status,
                    TicketPriority Priority,
                    DateTimeOffset OpenedOn,
                    DateTimeOffset LastMessageOn);

                public sealed record TicketMessageDto(
                    Guid Id,
                    MessageAuthorKind Author,
                    MessageVisibility Visibility,
                    string Body,
                    DateTimeOffset SentOn);

                public sealed record TicketDto(
                    Guid Id,
                    string Subject,
                    TicketStatus Status,
                    TicketPriority Priority,
                    DateTimeOffset OpenedOn,
                    Guid? LinkedFromTicketId,
                    IReadOnlyList<TicketMessageDto> Messages);
            }
            """;

        public const string OpenTicket = """
            namespace __NAME__.Application.Support.OpenTicket
            {
                using Truss.Application;

                public sealed record OpenTicket(string Subject, string Body) : ICommand<Guid>;
            }
            """;

        public const string OpenTicketHandler = """
            namespace __NAME__.Application.Support.OpenTicket
            {
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Support;
                using __NAME__.Domain.Support.Ticket;
                using __NAME__.Domain.Support.Ticket.ValueObjects;
                using Truss.Application;

                public class OpenTicketHandler(ITicketRepository tickets, ICurrentUser currentUser, TimeProvider timeProvider) : ICommandHandler<OpenTicket, Guid>
                {
                    public Task<Guid> Handle(OpenTicket command, CancellationToken cancellationToken)
                    {
                        var ticket = Ticket.Open(
                            currentUser.Require(),
                            TicketSubject.Create(command.Subject),
                            MessageBody.Create(command.Body),
                            timeProvider.GetUtcNow());

                        tickets.Add(ticket);

                        return Task.FromResult(ticket.Id.Value);
                    }
                }
            }
            """;

        public const string OpenTicketValidator = """
            namespace __NAME__.Application.Support.OpenTicket
            {
                using FluentValidation;

                public class OpenTicketValidator : AbstractValidator<OpenTicket>
                {
                    public OpenTicketValidator()
                    {
                        RuleFor(command => command.Subject).NotEmpty().MaximumLength(200);
                        RuleFor(command => command.Body).NotEmpty().MaximumLength(10_000);
                    }
                }
            }
            """;

        public const string ReplyToMyTicket = """
            namespace __NAME__.Application.Support.ReplyToMyTicket
            {
                using Truss.Application;

                /// <summary>
                /// Returns the id of the ticket that received the reply: the same
                /// ticket normally, a new linked one when this one no longer
                /// accepts replies.
                /// </summary>
                public sealed record ReplyToMyTicket(Guid TicketId, string Body) : ICommand<Guid>;
            }
            """;

        public const string ReplyToMyTicketHandler = """
            namespace __NAME__.Application.Support.ReplyToMyTicket
            {
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Support;
                using __NAME__.Domain.Support.Ticket;
                using __NAME__.Domain.Support.Ticket.Rules;
                using __NAME__.Domain.Support.Ticket.ValueObjects;
                using Truss.Application;
                using Truss.Domain;

                public class ReplyToMyTicketHandler(ITicketRepository tickets, ICurrentUser currentUser, TimeProvider timeProvider) : ICommandHandler<ReplyToMyTicket, Guid>
                {
                    public async Task<Guid> Handle(ReplyToMyTicket command, CancellationToken cancellationToken)
                    {
                        var requester = currentUser.Require();
                        var ticket = await tickets.GetById(new TicketId(command.TicketId), cancellationToken);

                        BusinessRule.Check(new TicketMustExist(ticket is not null && ticket.RequesterId == requester));

                        var now = timeProvider.GetUtcNow();
                        var body = MessageBody.Create(command.Body);

                        if (ticket!.AcceptsCustomerReply(now))
                        {
                            ticket.CustomerReply(body, now);
                            return ticket.Id.Value;
                        }

                        // Closed conversations stay closed: the reply continues the
                        // history in a new ticket linked to this one.
                        var successor = Ticket.Open(requester, ticket.Subject, body, now, ticket.Id);
                        tickets.Add(successor);

                        return successor.Id.Value;
                    }
                }
            }
            """;

        public const string ReplyToMyTicketValidator = """
            namespace __NAME__.Application.Support.ReplyToMyTicket
            {
                using FluentValidation;

                public class ReplyToMyTicketValidator : AbstractValidator<ReplyToMyTicket>
                {
                    public ReplyToMyTicketValidator()
                    {
                        RuleFor(command => command.TicketId).NotEmpty();
                        RuleFor(command => command.Body).NotEmpty().MaximumLength(10_000);
                    }
                }
            }
            """;

        public const string ListMyTickets = """
            namespace __NAME__.Application.Support.ListMyTickets
            {
                using __NAME__.Application.Support.DTOs;
                using Truss.Application;

                public sealed record ListMyTickets(int Page = 1, int Size = 20) : IQuery<PageResult<TicketSummaryDto>>;
            }
            """;

        public const string ListMyTicketsHandler = """
            namespace __NAME__.Application.Support.ListMyTickets
            {
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Support;
                using __NAME__.Application.Support.DTOs;
                using Truss.Application;

                public class ListMyTicketsHandler(ITicketRepository tickets, ICurrentUser currentUser) : IQueryHandler<ListMyTickets, PageResult<TicketSummaryDto>>
                {
                    public Task<PageResult<TicketSummaryDto>> Handle(ListMyTickets query, CancellationToken cancellationToken)
                    {
                        return tickets.ListFor(currentUser.Require(), new PageRequest(query.Page, query.Size), cancellationToken);
                    }
                }
            }
            """;

        public const string ListMyTicketsValidator = """
            namespace __NAME__.Application.Support.ListMyTickets
            {
                using FluentValidation;

                public class ListMyTicketsValidator : AbstractValidator<ListMyTickets>
                {
                    public ListMyTicketsValidator()
                    {
                        RuleFor(query => query.Page).GreaterThan(0);
                        RuleFor(query => query.Size).InclusiveBetween(1, 100);
                    }
                }
            }
            """;

        public const string GetMyTicket = """
            namespace __NAME__.Application.Support.GetMyTicket
            {
                using __NAME__.Application.Support.DTOs;
                using Truss.Application;

                public sealed record GetMyTicket(Guid TicketId) : IQuery<TicketDto?>;
            }
            """;

        public const string GetMyTicketHandler = """
            namespace __NAME__.Application.Support.GetMyTicket
            {
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Support;
                using __NAME__.Application.Support.DTOs;
                using __NAME__.Domain.Support.Ticket.ValueObjects;
                using Truss.Application;

                public class GetMyTicketHandler(ITicketRepository tickets, ICurrentUser currentUser) : IQueryHandler<GetMyTicket, TicketDto?>
                {
                    public Task<TicketDto?> Handle(GetMyTicket query, CancellationToken cancellationToken)
                    {
                        return tickets.GetDetail(new TicketId(query.TicketId), currentUser.Require(), cancellationToken);
                    }
                }
            }
            """;

        public const string ListSupportQueue = """
            namespace __NAME__.Application.Support.ListSupportQueue
            {
                using __NAME__.Application.Support.DTOs;
                using __NAME__.Domain.Support.Ticket;
                using Truss.Application;

                /// <summary>
                /// The staff queue. Without a status it lists everything still
                /// open in some sense (everything but Closed), oldest reply first.
                /// </summary>
                public sealed record ListSupportQueue(TicketStatus? Status = null, int Page = 1, int Size = 20) : IQuery<PageResult<TicketSummaryDto>>;
            }
            """;

        public const string ListSupportQueueHandler = """
            namespace __NAME__.Application.Support.ListSupportQueue
            {
                using __NAME__.Application.Support;
                using __NAME__.Application.Support.DTOs;
                using Truss.Application;

                public class ListSupportQueueHandler(ITicketRepository tickets) : IQueryHandler<ListSupportQueue, PageResult<TicketSummaryDto>>
                {
                    public Task<PageResult<TicketSummaryDto>> Handle(ListSupportQueue query, CancellationToken cancellationToken)
                    {
                        return tickets.Queue(query.Status, new PageRequest(query.Page, query.Size), cancellationToken);
                    }
                }
            }
            """;

        public const string ListSupportQueueValidator = """
            namespace __NAME__.Application.Support.ListSupportQueue
            {
                using FluentValidation;

                public class ListSupportQueueValidator : AbstractValidator<ListSupportQueue>
                {
                    public ListSupportQueueValidator()
                    {
                        RuleFor(query => query.Page).GreaterThan(0);
                        RuleFor(query => query.Size).InclusiveBetween(1, 100);
                    }
                }
            }
            """;

        public const string GetTicketForStaff = """
            namespace __NAME__.Application.Support.GetTicketForStaff
            {
                using __NAME__.Application.Support.DTOs;
                using Truss.Application;

                public sealed record GetTicketForStaff(Guid TicketId) : IQuery<TicketDto?>;
            }
            """;

        public const string GetTicketForStaffHandler = """
            namespace __NAME__.Application.Support.GetTicketForStaff
            {
                using __NAME__.Application.Support;
                using __NAME__.Application.Support.DTOs;
                using __NAME__.Domain.Support.Ticket.ValueObjects;
                using Truss.Application;

                public class GetTicketForStaffHandler(ITicketRepository tickets) : IQueryHandler<GetTicketForStaff, TicketDto?>
                {
                    public Task<TicketDto?> Handle(GetTicketForStaff query, CancellationToken cancellationToken)
                    {
                        return tickets.GetDetail(new TicketId(query.TicketId), null, cancellationToken);
                    }
                }
            }
            """;

        public const string ReplyAsStaff = """
            namespace __NAME__.Application.Support.ReplyAsStaff
            {
                using Truss.Application;

                public sealed record ReplyAsStaff(Guid TicketId, string Body, bool Internal = false) : ICommand;
            }
            """;

        public const string ReplyAsStaffHandler = """
            namespace __NAME__.Application.Support.ReplyAsStaff
            {
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Support;
                using __NAME__.Domain.Support.Ticket;
                using __NAME__.Domain.Support.Ticket.Rules;
                using __NAME__.Domain.Support.Ticket.ValueObjects;
                using Truss.Application;
                using Truss.Domain;

                public class ReplyAsStaffHandler(ITicketRepository tickets, ICurrentUser currentUser, TimeProvider timeProvider) : ICommandHandler<ReplyAsStaff>
                {
                    public async Task<Unit> Handle(ReplyAsStaff command, CancellationToken cancellationToken)
                    {
                        var ticket = await tickets.GetById(new TicketId(command.TicketId), cancellationToken);

                        BusinessRule.Check(new TicketMustExist(ticket is not null));

                        ticket!.StaffReply(
                            currentUser.Require(),
                            MessageBody.Create(command.Body),
                            command.Internal ? MessageVisibility.Internal : MessageVisibility.Public,
                            timeProvider.GetUtcNow());

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string ReplyAsStaffValidator = """
            namespace __NAME__.Application.Support.ReplyAsStaff
            {
                using FluentValidation;

                public class ReplyAsStaffValidator : AbstractValidator<ReplyAsStaff>
                {
                    public ReplyAsStaffValidator()
                    {
                        RuleFor(command => command.TicketId).NotEmpty();
                        RuleFor(command => command.Body).NotEmpty().MaximumLength(10_000);
                    }
                }
            }
            """;

        public const string ResolveTicket = """
            namespace __NAME__.Application.Support.ResolveTicket
            {
                using Truss.Application;

                public sealed record ResolveTicket(Guid TicketId) : ICommand;
            }
            """;

        public const string ResolveTicketHandler = """
            namespace __NAME__.Application.Support.ResolveTicket
            {
                using __NAME__.Application.Support;
                using __NAME__.Domain.Support.Ticket.Rules;
                using __NAME__.Domain.Support.Ticket.ValueObjects;
                using Truss.Application;
                using Truss.Domain;

                public class ResolveTicketHandler(ITicketRepository tickets, TimeProvider timeProvider) : ICommandHandler<ResolveTicket>
                {
                    public async Task<Unit> Handle(ResolveTicket command, CancellationToken cancellationToken)
                    {
                        var ticket = await tickets.GetById(new TicketId(command.TicketId), cancellationToken);

                        BusinessRule.Check(new TicketMustExist(ticket is not null));

                        ticket!.Resolve(timeProvider.GetUtcNow());

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string CloseTicket = """
            namespace __NAME__.Application.Support.CloseTicket
            {
                using Truss.Application;

                public sealed record CloseTicket(Guid TicketId) : ICommand;
            }
            """;

        public const string CloseTicketHandler = """
            namespace __NAME__.Application.Support.CloseTicket
            {
                using __NAME__.Application.Support;
                using __NAME__.Domain.Support.Ticket.Rules;
                using __NAME__.Domain.Support.Ticket.ValueObjects;
                using Truss.Application;
                using Truss.Domain;

                public class CloseTicketHandler(ITicketRepository tickets, TimeProvider timeProvider) : ICommandHandler<CloseTicket>
                {
                    public async Task<Unit> Handle(CloseTicket command, CancellationToken cancellationToken)
                    {
                        var ticket = await tickets.GetById(new TicketId(command.TicketId), cancellationToken);

                        BusinessRule.Check(new TicketMustExist(ticket is not null));

                        ticket!.Close(timeProvider.GetUtcNow());

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string SetTicketPriority = """
            namespace __NAME__.Application.Support.SetTicketPriority
            {
                using __NAME__.Domain.Support.Ticket;
                using Truss.Application;

                public sealed record SetTicketPriority(Guid TicketId, TicketPriority Priority) : ICommand;
            }
            """;

        public const string SetTicketPriorityHandler = """
            namespace __NAME__.Application.Support.SetTicketPriority
            {
                using __NAME__.Application.Support;
                using __NAME__.Domain.Support.Ticket.Rules;
                using __NAME__.Domain.Support.Ticket.ValueObjects;
                using Truss.Application;
                using Truss.Domain;

                public class SetTicketPriorityHandler(ITicketRepository tickets) : ICommandHandler<SetTicketPriority>
                {
                    public async Task<Unit> Handle(SetTicketPriority command, CancellationToken cancellationToken)
                    {
                        var ticket = await tickets.GetById(new TicketId(command.TicketId), cancellationToken);

                        BusinessRule.Check(new TicketMustExist(ticket is not null));

                        ticket!.SetPriority(command.Priority);

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string CloseExpiredTicketsJob = """
            namespace __NAME__.Application.Support.CloseExpiredTickets
            {
                using __NAME__.Application.Support;
                using __NAME__.Domain.Support;
                using Truss.Jobs;

                public sealed record CloseExpiredTicketsArgs;

                /// <summary>
                /// The auto-close sweep: resolved tickets whose reopen window has
                /// passed close for good. Scheduled hourly; each run commits
                /// through the job's unit of work.
                /// </summary>
                public class CloseExpiredTicketsJob(ITicketRepository tickets, TimeProvider timeProvider) : IJob<CloseExpiredTicketsArgs>
                {
                    public async Task Execute(CloseExpiredTicketsArgs args, JobContext context, CancellationToken cancellationToken)
                    {
                        var now = timeProvider.GetUtcNow();
                        var expired = await tickets.ResolvedBefore(now - SupportPolicy.ReopenWindow, limit: 200, cancellationToken);

                        foreach (var ticket in expired)
                            ticket.CloseIfExpired(now);
                    }
                }
            }
            """;

        public const string Configuration = """
            using __NAME__.Domain.Support.Ticket;
            using __NAME__.Domain.Support.Ticket.ValueObjects;
            using __NS_USERID__;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

            namespace __NAME__.Infrastructure.Support
            {
                public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
                {
                    // Timestamps persist as UTC ticks: DateTimeOffset is one of the
                    // named sqlite mines (not comparable in queries), and the test
                    // host runs on sqlite. A long compares everywhere.
                    private static readonly ValueConverter<DateTimeOffset, long> UtcTicks =
                        new(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));

                    public void Configure(EntityTypeBuilder<Ticket> builder)
                    {
                        builder.ToTable("SupportTickets");
                        builder.HasKey(ticket => ticket.Id);

                        builder.Property(ticket => ticket.Id)
                            .HasConversion(id => id.Value, value => new TicketId(value));

                        builder.Property(ticket => ticket.RequesterId)
                            .HasConversion(id => id.Value, value => new __USERID__(value));

                        builder.Property(ticket => ticket.Subject)
                            .HasConversion(subject => subject.Value, value => TicketSubject.Create(value))
                            .HasMaxLength(200);

                        builder.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(32);
                        builder.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(32);

                        builder.Property(ticket => ticket.LinkedFromTicketId)
                            .HasConversion(id => id!.Value, value => new TicketId(value));

                        builder.Property(ticket => ticket.OpenedOn).HasConversion(UtcTicks);
                        builder.Property(ticket => ticket.LastMessageOn).HasConversion(UtcTicks);
                        builder.Property(ticket => ticket.ResolvedOn).HasConversion(UtcTicks);
                        builder.Property(ticket => ticket.ClosedOn).HasConversion(UtcTicks);

                        builder.HasIndex(ticket => ticket.RequesterId);
                        builder.HasIndex(ticket => new { ticket.Status, ticket.LastMessageOn });

                        builder.OwnsMany(ticket => ticket.Messages, message =>
                        {
                            message.ToTable("SupportTicketMessages");
                            message.WithOwner().HasForeignKey("TicketId");
                            message.HasKey(entry => entry.Id);

                            message.Property(entry => entry.Id)
                                .HasConversion(id => id.Value, value => new TicketMessageId(value));

                            message.Property(entry => entry.AuthorId)
                                .HasConversion(id => id.Value, value => new __USERID__(value));

                            message.Property(entry => entry.AuthorKind).HasConversion<string>().HasMaxLength(32);
                            message.Property(entry => entry.Visibility).HasConversion<string>().HasMaxLength(32);

                            message.Property(entry => entry.Body)
                                .HasConversion(body => body.Value, value => MessageBody.Create(value))
                                .HasMaxLength(10_000);

                            message.Property(entry => entry.SentOn).HasConversion(UtcTicks);
                        });

                        builder.Navigation(ticket => ticket.Messages)
                            .UsePropertyAccessMode(PropertyAccessMode.Field);
                    }
                }
            }
            """;

        public const string EfRepository = """
            using __NAME__.Application.Support;
            using __NAME__.Application.Support.DTOs;
            using __NAME__.Domain.Support.Ticket;
            using __NAME__.Domain.Support.Ticket.ValueObjects;
            using __NS_USERID__;
            using Microsoft.EntityFrameworkCore;
            using Truss.Application;

            namespace __NAME__.Infrastructure.Support
            {
                public class EfTicketRepository(DbContext context) : ITicketRepository
                {
                    public void Add(Ticket ticket)
                    {
                        context.Set<Ticket>().Add(ticket);
                    }

                    public Task<Ticket?> GetById(TicketId id, CancellationToken cancellationToken = default)
                    {
                        return context.Set<Ticket>().FirstOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
                    }

                    public async Task<PageResult<TicketSummaryDto>> ListFor(__USERID__ requesterId, PageRequest page, CancellationToken cancellationToken = default)
                    {
                        // Converted members do not translate, so the page projects the
                        // converted properties whole and unwraps them in memory.
                        var result = await context.Set<Ticket>()
                            .AsNoTracking()
                            .Where(ticket => ticket.RequesterId == requesterId)
                            .OrderByDescending(ticket => ticket.LastMessageOn)
                            .Select(ticket => new { ticket.Id, ticket.Subject, ticket.Status, ticket.Priority, ticket.OpenedOn, ticket.LastMessageOn })
                            .ToPageAsync(page, cancellationToken);

                        return result.Map(ticket => new TicketSummaryDto(
                            ticket.Id.Value, ticket.Subject.Value, ticket.Status, ticket.Priority, ticket.OpenedOn, ticket.LastMessageOn));
                    }

                    public async Task<TicketDto?> GetDetail(TicketId id, __USERID__? requesterId, CancellationToken cancellationToken = default)
                    {
                        // No tracking: projecting the owned messages without their
                        // owner is not something a tracking query can shape.
                        var query = context.Set<Ticket>().AsNoTracking().Where(ticket => ticket.Id == id);

                        if (requesterId is not null)
                            query = query.Where(ticket => ticket.RequesterId == requesterId);

                        var ticket = await query
                            .Select(found => new
                            {
                                found.Id,
                                found.Subject,
                                found.Status,
                                found.Priority,
                                found.OpenedOn,
                                found.LinkedFromTicketId,
                                Messages = found.Messages
                                    .Where(message => requesterId == null || message.Visibility == MessageVisibility.Public)
                                    .OrderBy(message => message.SentOn)
                                    .ToList()
                            })
                            .FirstOrDefaultAsync(cancellationToken);

                        return ticket is null
                            ? null
                            : new TicketDto(
                                ticket.Id.Value,
                                ticket.Subject.Value,
                                ticket.Status,
                                ticket.Priority,
                                ticket.OpenedOn,
                                ticket.LinkedFromTicketId == null ? null : ticket.LinkedFromTicketId.Value,
                                [.. ticket.Messages.Select(message => new TicketMessageDto(
                                    message.Id.Value, message.AuthorKind, message.Visibility, message.Body.Value, message.SentOn))]);
                    }

                    public async Task<PageResult<TicketSummaryDto>> Queue(TicketStatus? status, PageRequest page, CancellationToken cancellationToken = default)
                    {
                        var query = status is null
                            ? context.Set<Ticket>().AsNoTracking().Where(ticket => ticket.Status != TicketStatus.Closed)
                            : context.Set<Ticket>().AsNoTracking().Where(ticket => ticket.Status == status);

                        var result = await query
                            .OrderBy(ticket => ticket.LastMessageOn)
                            .Select(ticket => new { ticket.Id, ticket.Subject, ticket.Status, ticket.Priority, ticket.OpenedOn, ticket.LastMessageOn })
                            .ToPageAsync(page, cancellationToken);

                        return result.Map(ticket => new TicketSummaryDto(
                            ticket.Id.Value, ticket.Subject.Value, ticket.Status, ticket.Priority, ticket.OpenedOn, ticket.LastMessageOn));
                    }

                    public async Task<IReadOnlyList<Ticket>> ResolvedBefore(DateTimeOffset cutoff, int limit, CancellationToken cancellationToken = default)
                    {
                        return await context.Set<Ticket>()
                            .Where(ticket => ticket.Status == TicketStatus.Resolved && ticket.ResolvedOn < cutoff)
                            .OrderBy(ticket => ticket.ResolvedOn)
                            .Take(limit)
                            .ToListAsync(cancellationToken);
                    }
                }
            }
            """;

        public const string SupportModule = """
            using __NAME__.Application.Support;
            using __NAME__.Infrastructure.Support;
            using Microsoft.Extensions.DependencyInjection;

            namespace __NAME__.Infrastructure
            {
                public static class SupportModule
                {
                    public static IServiceCollection AddSupportInfrastructure(this IServiceCollection services)
                    {
                        services.AddScoped<ITicketRepository, EfTicketRepository>();
                        return services;
                    }
                }
            }
            """;

        public const string ProgramUsings = """
            using __NAME__.Application.Support.CloseTicket;
            using __NAME__.Application.Support.DTOs;
            using __NAME__.Application.Support.GetMyTicket;
            using __NAME__.Application.Support.GetTicketForStaff;
            using __NAME__.Application.Support.ListMyTickets;
            using __NAME__.Application.Support.ListSupportQueue;
            using __NAME__.Application.Support.OpenTicket;
            using __NAME__.Application.Support.ReplyAsStaff;
            using __NAME__.Application.Support.ReplyToMyTicket;
            using __NAME__.Application.Support.ResolveTicket;
            using __NAME__.Application.Support.SetTicketPriority;
            using Truss.Application;
            """;

        public const string ProgramServices = """
            builder.Services.AddSupportInfrastructure();
            """;

        public const string ProgramEndpoints = """
            app.MapCommand<OpenTicket, Guid>("/support/tickets", id => $"/support/tickets/{id}").RequireAuthorization();
            app.MapCommand<ReplyToMyTicket, Guid>("/support/tickets/{ticketId:guid}/messages").RequireAuthorization();
            app.MapQuery<ListMyTickets, PageResult<TicketSummaryDto>>("/support/tickets").RequireAuthorization();
            app.MapQuery<GetMyTicket, TicketDto?>("/support/tickets/{ticketId:guid}").RequireAuthorization();
            app.MapQuery<ListSupportQueue, PageResult<TicketSummaryDto>>("/support/queue")__STAFF__;
            app.MapQuery<GetTicketForStaff, TicketDto?>("/support/queue/{ticketId:guid}")__STAFF__;
            app.MapCommand<ReplyAsStaff>("/support/queue/{ticketId:guid}/reply")__STAFF__;
            app.MapCommand<ResolveTicket>("/support/queue/{ticketId:guid}/resolve")__STAFF__;
            app.MapCommand<CloseTicket>("/support/queue/{ticketId:guid}/close")__STAFF__;
            app.MapPutCommand<SetTicketPriority>("/support/queue/{ticketId:guid}/priority")__STAFF__;
            """;

        public const string ProgramRecurringJob = """
            // The auto-close sweep runs hourly; the scheduler lock keeps a single
            // runner across instances.
            builder.Services.Configure<Truss.Jobs.TrussJobsOptions>(options =>
                options.AddRecurring<__NAME__.Application.Support.CloseExpiredTickets.CloseExpiredTicketsJob, __NAME__.Application.Support.CloseExpiredTickets.CloseExpiredTicketsArgs>(
                    "0 * * * *", new __NAME__.Application.Support.CloseExpiredTickets.CloseExpiredTicketsArgs()));
            """;


        public const string DeckRequesterSource = """
            using Truss.Support;

            namespace __NAME__.Application.Support
            {
                /// <summary>
                /// Who the deck should see behind this request. The default pulls
                /// the signed-in account; edit it freely if your requester's
                /// display data lives elsewhere.
                /// </summary>
                public interface ISupportRequesterSource
                {
                    Task<SupportRequester> Current(CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string DeckAccountRequesterSource = """
            using __NAME__.Application.Accounts;
            using Truss.Support;

            namespace __NAME__.Application.Support
            {
                public class AccountRequesterSource(ICurrentUser currentUser, IUserRepository users) : ISupportRequesterSource
                {
                    public async Task<SupportRequester> Current(CancellationToken cancellationToken = default)
                    {
                        var id = currentUser.Require();

                        var user = await users.GetById(id, cancellationToken)
                            ?? throw new InvalidOperationException("The signed-in account was not found.");

                        return new SupportRequester(id.Value.ToString(), user.Email, user.Name);
                    }
                }
            }
            """;

        public const string DeckOpenTicket = """
            namespace __NAME__.Application.Support.OpenTicket
            {
                using Truss.Application;

                public sealed record OpenTicket(string Subject, string Body) : ICommand<Guid>;
            }
            """;

        public const string DeckOpenTicketHandler = """
            namespace __NAME__.Application.Support.OpenTicket
            {
                using __NAME__.Application.Support;
                using Truss.Application;
                using Truss.Support;

                public class OpenTicketHandler(ISupportDeckClient deck, ISupportRequesterSource requester) : ICommandHandler<OpenTicket, Guid>
                {
                    public async Task<Guid> Handle(OpenTicket command, CancellationToken cancellationToken)
                    {
                        return await deck.OpenTicket(
                            await requester.Current(cancellationToken), command.Subject, command.Body, metadata: null, cancellationToken);
                    }
                }
            }
            """;

        public const string DeckOpenTicketValidator = """
            namespace __NAME__.Application.Support.OpenTicket
            {
                using FluentValidation;

                public class OpenTicketValidator : AbstractValidator<OpenTicket>
                {
                    public OpenTicketValidator()
                    {
                        RuleFor(command => command.Subject).NotEmpty().MaximumLength(200);
                        RuleFor(command => command.Body).NotEmpty().MaximumLength(10_000);
                    }
                }
            }
            """;

        public const string DeckReply = """
            namespace __NAME__.Application.Support.ReplyToMyTicket
            {
                using Truss.Application;

                /// <summary>
                /// Returns the id of the ticket that received the reply: the same
                /// ticket normally, a new linked one when the deck no longer
                /// accepts replies there.
                /// </summary>
                public sealed record ReplyToMyTicket(Guid TicketId, string Body) : ICommand<Guid>;
            }
            """;

        public const string DeckReplyHandler = """
            namespace __NAME__.Application.Support.ReplyToMyTicket
            {
                using __NAME__.Application.Support;
                using Truss.Application;
                using Truss.Support;

                public class ReplyToMyTicketHandler(ISupportDeckClient deck, ISupportRequesterSource requester) : ICommandHandler<ReplyToMyTicket, Guid>
                {
                    public async Task<Guid> Handle(ReplyToMyTicket command, CancellationToken cancellationToken)
                    {
                        return await deck.Reply(
                            command.TicketId, await requester.Current(cancellationToken), command.Body, cancellationToken);
                    }
                }
            }
            """;

        public const string DeckReplyValidator = """
            namespace __NAME__.Application.Support.ReplyToMyTicket
            {
                using FluentValidation;

                public class ReplyToMyTicketValidator : AbstractValidator<ReplyToMyTicket>
                {
                    public ReplyToMyTicketValidator()
                    {
                        RuleFor(command => command.TicketId).NotEmpty();
                        RuleFor(command => command.Body).NotEmpty().MaximumLength(10_000);
                    }
                }
            }
            """;

        public const string DeckList = """
            namespace __NAME__.Application.Support.ListMyTickets
            {
                using Truss.Application;
                using Truss.Support;

                public sealed record ListMyTickets(int Page = 1, int Size = 20) : IQuery<PageResult<SupportTicketSummary>>;
            }
            """;

        public const string DeckListHandler = """
            namespace __NAME__.Application.Support.ListMyTickets
            {
                using __NAME__.Application.Support;
                using Truss.Application;
                using Truss.Support;

                public class ListMyTicketsHandler(ISupportDeckClient deck, ISupportRequesterSource requester) : IQueryHandler<ListMyTickets, PageResult<SupportTicketSummary>>
                {
                    public async Task<PageResult<SupportTicketSummary>> Handle(ListMyTickets query, CancellationToken cancellationToken)
                    {
                        var current = await requester.Current(cancellationToken);
                        return await deck.ListTickets(current.ExternalUserId, query.Page, query.Size, cancellationToken);
                    }
                }
            }
            """;

        public const string DeckListValidator = """
            namespace __NAME__.Application.Support.ListMyTickets
            {
                using FluentValidation;

                public class ListMyTicketsValidator : AbstractValidator<ListMyTickets>
                {
                    public ListMyTicketsValidator()
                    {
                        RuleFor(query => query.Page).GreaterThan(0);
                        RuleFor(query => query.Size).InclusiveBetween(1, 100);
                    }
                }
            }
            """;

        public const string DeckGet = """
            namespace __NAME__.Application.Support.GetMyTicket
            {
                using Truss.Application;
                using Truss.Support;

                public sealed record GetMyTicket(Guid TicketId) : IQuery<SupportTicket?>;
            }
            """;

        public const string DeckGetHandler = """
            namespace __NAME__.Application.Support.GetMyTicket
            {
                using __NAME__.Application.Support;
                using Truss.Application;
                using Truss.Support;

                public class GetMyTicketHandler(ISupportDeckClient deck, ISupportRequesterSource requester) : IQueryHandler<GetMyTicket, SupportTicket?>
                {
                    public async Task<SupportTicket?> Handle(GetMyTicket query, CancellationToken cancellationToken)
                    {
                        var current = await requester.Current(cancellationToken);
                        return await deck.GetTicket(query.TicketId, current.ExternalUserId, cancellationToken);
                    }
                }
            }
            """;

        public const string DeckProgramUsings = """
            using __NAME__.Application.Support;
            using __NAME__.Application.Support.GetMyTicket;
            using __NAME__.Application.Support.ListMyTickets;
            using __NAME__.Application.Support.OpenTicket;
            using __NAME__.Application.Support.ReplyToMyTicket;
            using Truss.Application;
            using Truss.Support;
            """;

        public const string DeckProgramServices = """
            builder.Services.AddTrussSupportDeck(options => builder.Configuration.GetSection("Truss:Support:Deck").Bind(options));
            builder.Services.AddScoped<ISupportRequesterSource, AccountRequesterSource>();
            builder.Services.AddScoped<SupportNotificationHandler>();
            """;

        public const string DeckProgramEndpoints = """
            app.MapCommand<OpenTicket, Guid>("/support/tickets", id => $"/support/tickets/{id}").RequireAuthorization();
            app.MapCommand<ReplyToMyTicket, Guid>("/support/tickets/{ticketId:guid}/messages").RequireAuthorization();
            app.MapQuery<ListMyTickets, PageResult<SupportTicketSummary>>("/support/tickets").RequireAuthorization();
            app.MapQuery<GetMyTicket, SupportTicket?>("/support/tickets/{ticketId:guid}").RequireAuthorization();
            """;


        public const string DeckNotificationHandlerEmail = """
            using __NAME__.Application.Accounts;
            using Truss.Email;
            using Truss.Support;

            namespace __NAME__.Application.Support
            {
                /// <summary>
                /// What happens when the deck notifies this application. The
                /// default emails the requester through your sender; edit freely.
                /// Notifications are cosmetic: the message lives on the deck, so
                /// doing nothing here loses nothing.
                /// </summary>
                public class SupportNotificationHandler(IUserRepository users, IEmailSender email)
                {
                    public async Task Handle(SupportWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
                    {
                        if (webhookEvent.Type != "deck.support.agent-replied" || !Guid.TryParse(webhookEvent.ExternalUserId, out var userId))
                            return;

                        var user = await users.GetById(new(userId), cancellationToken);

                        if (user is null)
                            return;

                        await email.Send(new EmailMessage(
                            user.Email,
                            $"Your ticket has a new answer: {webhookEvent.Subject}",
                            "<p>Support answered your ticket. Open the app to read the reply.</p>"), cancellationToken);
                    }
                }
            }
            """;

        public const string DeckNotificationHandlerLog = """
            using Microsoft.Extensions.Logging;
            using Truss.Support;

            namespace __NAME__.Application.Support
            {
                /// <summary>
                /// What happens when the deck notifies this application. Without
                /// the email module there is no channel to reach the requester, so
                /// the default only logs; the requester sees the answer on their
                /// next visit. Notifications are cosmetic: nothing is lost here.
                /// </summary>
                public class SupportNotificationHandler(ILogger<SupportNotificationHandler> logger)
                {
                    public Task Handle(SupportWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
                    {
                        logger.LogInformation(
                            "The deck sent {Type} for ticket {TicketId}.", webhookEvent.Type, webhookEvent.TicketId);

                        return Task.CompletedTask;
                    }
                }
            }
            """;

        public const string DeckWebhookEndpoint = """
            app.MapPost("/support/deck-webhook", async (HttpRequest request, SupportNotificationHandler handler, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(cancellationToken);

                // Fail closed: without the secret, or with a signature that does
                // not match the raw body, the delivery is nobody's business.
                if (!SupportWebhook.TryParse(body, request.Headers[SupportWebhook.SignatureHeader], configuration["Truss:Support:Deck:WebhookSecret"] ?? string.Empty, out var webhookEvent))
                    return Results.Unauthorized();

                await handler.Handle(webhookEvent!, cancellationToken);
                return Results.NoContent();
            });
            """;


        public const string DeckAttachmentEndpoints = """
            app.MapPost("/support/tickets/{ticketId:guid}/attachments", async (Guid ticketId, IFormFile file, ISupportDeckClient deck, ISupportRequesterSource requester, CancellationToken cancellationToken) =>
            {
                var current = await requester.Current(cancellationToken);

                await using var content = file.OpenReadStream();
                var receipt = await deck.UploadAttachment(ticketId, current.ExternalUserId, file.FileName, file.ContentType, content, cancellationToken);

                return Results.Created((string?)null, receipt);
            }).DisableAntiforgery().RequireAuthorization().AddTrussErrorHandling();

            app.MapGet("/support/tickets/{ticketId:guid}/attachments/{attachmentId:guid}", async (Guid ticketId, Guid attachmentId, HttpContext http, ISupportDeckClient deck, ISupportRequesterSource requester, CancellationToken cancellationToken) =>
            {
                var current = await requester.Current(cancellationToken);
                var download = await deck.DownloadAttachment(ticketId, attachmentId, current.ExternalUserId, cancellationToken);

                if (download is null)
                    return Results.NotFound();

                // Files leave as attachments with sniffing disabled, here like
                // on the deck: evidence, never markup.
                http.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.Stream(download.Content, download.ContentType, download.FileName);
            }).RequireAuthorization().AddTrussErrorHandling();
            """;

        public const string DomainTests = """
            using __NAME__.Domain.Support;
            using __NAME__.Domain.Support.Ticket;
            using __NAME__.Domain.Support.Ticket.ValueObjects;
            using __NS_USERID__;
            using Truss.Domain;
            using Xunit;

            namespace __NAME__.Domain.Tests.Support
            {
                public class TicketTests
                {
                    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
                    private static readonly __USERID__ Requester = new(Guid.NewGuid());
                    private static readonly __USERID__ Staff = new(Guid.NewGuid());

                    private static Ticket OpenTicket()
                    {
                        return Ticket.Open(Requester, TicketSubject.Create("The export is broken"), MessageBody.Create("It fails with a 500."), Now);
                    }

                    [Fact]
                    public void Open_StartsTheConversation()
                    {
                        var ticket = OpenTicket();

                        Assert.Equal(TicketStatus.Open, ticket.Status);
                        Assert.Single(ticket.Messages);
                        Assert.Equal(Requester, ticket.RequesterId);
                    }

                    [Fact]
                    public void StaffPublicReply_HandsTheBallToTheCustomer()
                    {
                        var ticket = OpenTicket();

                        ticket.StaffReply(Staff, MessageBody.Create("Which file?"), MessageVisibility.Public, Now.AddMinutes(5));

                        Assert.Equal(TicketStatus.WaitingOnCustomer, ticket.Status);
                    }

                    [Fact]
                    public void CustomerReply_HandsTheBallBack()
                    {
                        var ticket = OpenTicket();
                        ticket.StaffReply(Staff, MessageBody.Create("Which file?"), MessageVisibility.Public, Now.AddMinutes(5));

                        ticket.CustomerReply(MessageBody.Create("The yearly report."), Now.AddMinutes(10));

                        Assert.Equal(TicketStatus.Open, ticket.Status);
                    }

                    [Fact]
                    public void CustomerReply_WithinTheWindow_ReopensAResolvedTicket()
                    {
                        var ticket = OpenTicket();
                        ticket.Resolve(Now.AddHours(1));

                        ticket.CustomerReply(MessageBody.Create("It broke again."), Now.AddDays(2));

                        Assert.Equal(TicketStatus.Open, ticket.Status);
                        Assert.Null(ticket.ResolvedOn);
                    }

                    [Fact]
                    public void CustomerReply_AfterTheWindow_IsRefused()
                    {
                        var ticket = OpenTicket();
                        ticket.Resolve(Now.AddHours(1));

                        var afterWindow = Now.AddHours(1) + SupportPolicy.ReopenWindow + TimeSpan.FromDays(1);

                        Assert.False(ticket.AcceptsCustomerReply(afterWindow));
                        Assert.Throws<BusinessRuleValidationException>(
                            () => ticket.CustomerReply(MessageBody.Create("Anyone there?"), afterWindow));
                    }

                    [Fact]
                    public void ResolvedTicket_TakesInternalNotes_ButNoPublicReplies()
                    {
                        var ticket = OpenTicket();
                        ticket.Resolve(Now.AddHours(1));

                        ticket.StaffReply(Staff, MessageBody.Create("Root cause was the cache."), MessageVisibility.Internal, Now.AddHours(2));

                        Assert.Throws<BusinessRuleValidationException>(
                            () => ticket.StaffReply(Staff, MessageBody.Create("Fixed."), MessageVisibility.Public, Now.AddHours(2)));
                        Assert.Equal(TicketStatus.Resolved, ticket.Status);
                    }

                    [Fact]
                    public void CloseIfExpired_ClosesOnlyPastTheWindow()
                    {
                        var ticket = OpenTicket();
                        ticket.Resolve(Now);

                        Assert.False(ticket.CloseIfExpired(Now + SupportPolicy.ReopenWindow - TimeSpan.FromHours(1)));
                        Assert.True(ticket.CloseIfExpired(Now + SupportPolicy.ReopenWindow + TimeSpan.FromHours(1)));
                        Assert.Equal(TicketStatus.Closed, ticket.Status);
                    }
                }
            }
            """;

        public const string IntegrationTests = """
            using __NAME__.Application;
            using __NAME__.Application.Accounts;
            using __NAME__.Application.Support.GetMyTicket;
            using __NAME__.Application.Support.ListMyTickets;
            using __NAME__.Application.Support.OpenTicket;
            using __NAME__.Application.Support.ReplyAsStaff;
            using __NAME__.Application.Support.ReplyToMyTicket;
            using __NAME__.Application.Support.ResolveTicket;
            using __NAME__.Domain.Support.Ticket;
            using __NAME__.Infrastructure;
            using __NS_USERID__;
            using Microsoft.Extensions.DependencyInjection;
            using Truss.Testing;
            using Xunit;

            namespace __NAME__.IntegrationTests.Support
            {
                /// <summary>
                /// The current user of the test host: whoever the test says it is.
                /// </summary>
                public sealed class TestCurrentUser : ICurrentUser
                {
                    public __USERID__? Id { get; set; }

                    public bool IsAuthenticated => Id is not null;

                    public __USERID__ Require() => Id ?? throw new InvalidOperationException("The test did not set a current user.");
                }

                public class SupportTests
                {
                    private static async Task<(TrussTestHost Host, TestCurrentUser CurrentUser)> StartHost()
                    {
                        var currentUser = new TestCurrentUser { Id = new __USERID__(Guid.NewGuid()) };

                        var host = await TrussTestHost.Start<AppDbContext>(options =>
                        {
                            options.AddAssembly<ApplicationAssemblyMarker>();
                            options.ConfigureServices(services =>
                            {
                                services.AddSupportInfrastructure();
                                services.AddSingleton<ICurrentUser>(currentUser);
                            });
                        });

                        return (host, currentUser);
                    }

                    [Fact]
                    public async Task OpenTicket_ShowsUpInMyTickets()
                    {
                        var (host, _) = await StartHost();
                        await using var _1 = host;

                        var id = await host.Send(new OpenTicket("The export is broken", "It fails with a 500."));
                        var mine = await host.Send(new ListMyTickets());

                        var summary = Assert.Single(mine.Items);
                        Assert.Equal(id, summary.Id);
                        Assert.Equal(TicketStatus.Open, summary.Status);
                    }

                    [Fact]
                    public async Task InternalNotes_NeverReachTheRequester()
                    {
                        var (host, _) = await StartHost();
                        await using var _1 = host;

                        var id = await host.Send(new OpenTicket("Billing question", "Was I charged twice?"));
                        await host.Send(new ReplyAsStaff(id, "Check the gateway logs.", Internal: true));
                        await host.Send(new ReplyAsStaff(id, "You were charged once; the second entry is a hold."));

                        var ticket = await host.Send(new GetMyTicket(id));

                        Assert.Equal(2, ticket!.Messages.Count);
                        Assert.DoesNotContain(ticket.Messages, message => message.Body.Contains("gateway"));
                        Assert.Equal(TicketStatus.WaitingOnCustomer, ticket.Status);
                    }

                    [Fact]
                    public async Task ReplyToAResolvedTicket_ReopensIt()
                    {
                        var (host, _) = await StartHost();
                        await using var _1 = host;

                        var id = await host.Send(new OpenTicket("Login loops", "The app logs me out."));
                        await host.Send(new ResolveTicket(id));

                        var repliedTo = await host.Send(new ReplyToMyTicket(id, "It happened again."));
                        var ticket = await host.Send(new GetMyTicket(id));

                        Assert.Equal(id, repliedTo);
                        Assert.Equal(TicketStatus.Open, ticket!.Status);
                    }

                    [Fact]
                    public async Task SomebodyElsesTicket_DoesNotExistForMe()
                    {
                        var (host, currentUser) = await StartHost();
                        await using var _1 = host;

                        var id = await host.Send(new OpenTicket("Mine", "My private problem."));

                        currentUser.Id = new __USERID__(Guid.NewGuid());

                        Assert.Null(await host.Send(new GetMyTicket(id)));
                    }
                }
            }
            """;
    }
}
