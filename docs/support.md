# User Support

`truss add support` scaffolds a complete support desk into your application:
a Ticket aggregate with a real state machine, customer routes, staff routes
and tests. Like the account slice, it is **your code once written**: edit the
rules, the routes and the policy freely.

```
truss add support
```

Requires a database and the auth module (tickets belong to signed-in users).
This is the standalone mode: tickets live in your application's database. The
centralized mode, where a fleet of applications shares one attendance surface
through Truss Deck, arrives later in the 0.6 line.

---

## The Ticket's Life

```
Open  <----------------------->  WaitingOnCustomer
  |     staff reply / customer reply     |
  +------------- staff resolves ---------+
                    |
                 Resolved --- customer replies within the window ---> Open
                    |
                 (window passes, hourly sweep or staff)
                    |
                 Closed  --- customer replies ---> a NEW ticket, linked
```

The graph is a business rule and lives on the aggregate; the reopen window is
a preference and lives in `SupportPolicy.cs` (default: 7 days). A closed
conversation stays closed: a reply after the window opens a new ticket
carrying `LinkedFromTicketId`, so history never reopens but is never lost.

Priority (`Normal`, `High`, `Urgent`) is triage: staff sets it, the requester
never does.

---

## Routes

Customer surface, authenticated:

```
POST /support/tickets                    open a ticket
POST /support/tickets/{id}/messages      reply (returns the ticket that took it)
GET  /support/tickets                    my tickets, paged
GET  /support/tickets/{id}               one conversation, internal notes hidden
```

Staff surface, protected with `support.manage` when rbac is installed
(authentication only otherwise, and the install says so):

```
GET  /support/queue                      the queue, oldest reply first
GET  /support/queue/{id}                 one conversation, notes included
POST /support/queue/{id}/reply           reply ({ "internal": true } for a note)
POST /support/queue/{id}/resolve
POST /support/queue/{id}/close
PUT  /support/queue/{id}/priority
```

A reply to a ticket that no longer accepts one answers with the id of the new
linked ticket, so clients follow the conversation without special cases.

---

## Internal Notes

A staff reply with `"internal": true` is a note between staff: it never
appears on the customer surface, and a resolved ticket accepts only notes
(a public staff reply on a resolved ticket would reopen a conversation on the
staff's initiative).

Message bodies are plain text on every surface. They are never rendered as
markup; treating them as HTML would hand the requester a script injection
into the staff screen.

---

## The Auto-Close Sweep

With the jobs module installed, an hourly recurring job closes resolved
tickets whose reopen window has passed; the scheduler lock keeps a single
runner across instances. Without jobs, tickets close when staff closes them,
and the install says so.

---

## What the Scaffold Writes

- `Domain/Support`: `SupportPolicy` and the `Ticket` aggregate with its
  messages, value objects, events and rules. Timestamps persist as UTC ticks,
  which keeps date comparisons working on sqlite under the test host.
- `Application/Support`: the customer and staff slices, `ITicketRepository`
  and the DTOs.
- `Infrastructure/Support`: the EF configuration and repository, registered
  by `AddSupportInfrastructure()` in every host, the worker included.
- Tests: the state machine in the domain suite, and integration tests through
  the real pipeline, including one proving internal notes never reach the
  requester and one proving another user's ticket answers as missing.
