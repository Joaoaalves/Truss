# Common Pitfalls

- **Calling `SaveChangesAsync` inside a command handler.** The unit of work already commits after the handler succeeds. A manual save creates a second commit and dispatches domain events at the wrong moment.
- **Changing state inside a query handler.** Queries have no unit of work. Changes are either lost or leak through an unrelated commit.
- **Treating domain events as integration events.** Domain event handlers run inside the transaction and fail the command when they throw. Side effects that must survive on their own, such as e-mails or broker messages, belong to integration events (see the [Roadmap](roadmap.md)).
- **Forgetting the assembly registration.** Handlers and validators are discovered only from assemblies given to `AddTruss`. A missing registration surfaces as a missing handler exception at dispatch time.
- **Anemic entities.** If every rule lives in handlers, entities become data bags and invariants scatter across the codebase.
- **Raising events for technical noise.** "Updated" is rarely a domain event. "OrderPlaced" is.

Truss encourages explicit modeling but does not prevent misuse.
