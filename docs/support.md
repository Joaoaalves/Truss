# User Support

`truss add support` scaffolds a complete support desk into your application:
a Ticket aggregate with a real state machine, customer routes, staff routes
and tests. Like the account slice, it is **your code once written**: edit the
rules, the routes and the policy freely.

```
truss add support
```

Requires the auth module (tickets belong to signed-in users). Two modes share
the same four customer routes, so switching never changes your public API:

- **Standalone** (`truss add support`): the whole desk lives in your
  application: the Ticket aggregate, the staff routes, your database.
  Requires a database.
- **Deck** (`truss add support --deck <url>`): your application keeps only
  the thin customer surface; tickets live on the [Truss Deck](#the-deck-mode),
  where one team attends a whole fleet of applications.

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

---

## The Deck Mode

```
truss add support --deck https://deck.example.com
```

With a deck, the application stores nothing: the scaffolded handlers forward
to the deck's ingestion API through the typed client in `Truss.Support`. What
the install writes:

- The same four customer routes, now backed by `ISupportDeckClient`.
- `AccountRequesterSource`, your code: it tells the deck who is behind the
  request (the signed-in account's id, email and name). Edit it if your
  display data lives elsewhere.
- `Truss:Support:Deck:Url` in appsettings; the key never goes there. Register
  the application on the deck, take the key it answers **once**, and set it
  per environment: `Truss__Support__Deck__ApiKey`. `truss deploy check`
  demands it.

Every call carries the service credential; writes carry an idempotency key,
so a retried request can never duplicate a ticket or a message. Failures come
back as the exceptions your pipeline already speaks: validation problems and
broken rules rebuild locally with their stable codes, and an unreachable deck
throws `SupportDeckException` naming the operation and the address. The
support surface degrades; the rest of the application does not care.

There are no staff routes in this mode: attendance happens on the deck, where
agents see the whole fleet's queue, filter by application, keep internal
notes and carry permissions per app.

### Attachments

Files ride the same routes on both sides: the customer uploads through
`POST /support/tickets/{id}/attachments` (multipart) and downloads through
`GET .../attachments/{attachmentId}`; the deck stores the bytes in an
S3-compatible store and answers whether a malware scan still holds the file.
Every upload passes a structural gate before a byte is stored: an allowlist
of content types (images, pdf, plain text and csv) with magic bytes that
must match, SVG and HTML refused on purpose. Downloads leave as attachments
with sniffing disabled, only when available; missing, scanning and rejected
answer the same 404. Caps: 10 MB per file, 20 files per ticket.

### Read Receipts and the Offline Queue

Summaries carry an `unread` badge: any public agent message after the
requester's last read receipt. `POST /support/tickets/{id}/read` clears it;
internal notes never light it.

With the jobs module installed, a ticket or reply typed while the deck is
unreachable is queued through the job runtime and delivered when the deck
answers again. A queued reply changes nothing for the caller; a queued
opening returns the submission's id rather than the ticket's, and the
scaffolded handler says so in place, with the fallback yours to remove if
you prefer an honest 502 over the ambiguity.

### Notifications

When an agent answers or resolves, the deck notifies the application with a
signed webhook: `POST /fleet/apps/{id}/webhook` on the deck sets the
destination and answers the signing secret exactly once; set it as
`Truss__Support__Deck__WebhookSecret`. The scaffolded receiver at
`/support/deck-webhook` verifies the HMAC of the raw body (fail closed) and
hands the event to `SupportNotificationHandler`, your code: with the email
module it mails the requester; without it, it logs. Deliveries ride the
deck's outbox with retry and dead-letter, and internal notes never leave the
deck. Notifications are cosmetic by contract: the message lives on the deck,
so a missed webhook delays an email and loses nothing.

---

## Migrating From Standalone to the Deck

The two modes share their routes, so the migration never touches your
clients. The honest sequence:

1. Register the application on the deck; keep the key.
2. Export the standalone tickets (the `SupportTickets` and
   `SupportTicketMessages` tables) and import them through the deck's
   ingestion API or directly into its database, mapping your `RequesterId`
   to `ExternalUserId`. History is worth moving; the state machine is the
   same on both sides.
3. Remove the local Support context (`truss remove context Support` cleans
   the wiring) and run `truss add support --deck <url>`.
4. Set `Truss__Support__Deck__ApiKey` in every environment and drop the
   local tables with your next migration.

Moving back is the same walk in reverse; a "no" to the deck is never final.
