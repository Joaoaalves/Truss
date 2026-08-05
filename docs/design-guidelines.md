# Design Guidelines

- Keep domain logic inside the domain. Handlers orchestrate; aggregates decide.
- Enforce invariants with business rules inside entities and value objects, not in handlers.
- Use value objects aggressively. Primitive obsession hides domain concepts.
- Give every aggregate a typed identifier.
- Raise domain events only for meaningful business occurrences.
- Treat aggregate roots as transactional boundaries. One command, one aggregate, whenever possible.
- Return ids or DTOs from handlers, never entities.
- Never inject `IUnitOfWork` into application code. The pipeline owns the transaction.
- Validate input shape with validators. Protect invariants with business rules. These are different concerns and different failure types.
