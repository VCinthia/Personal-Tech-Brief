# ADR-008 — Use a transactional outbox for DB-to-Service-Bus handoff

**Status:** Accepted

## Context

Ingestion must persist a new source item and eventually enqueue it. Saving to SQL and publishing to Service Bus as two independent operations creates a failure window where one succeeds and the other does not.

## Decision

Persist the SourceItem and an OutboxMessage in the same SQL transaction. A dispatcher publishes pending outbox messages and marks them dispatched only after successful broker publication.

Use stable message IDs and idempotent consumers.

## Consequences

- closes the dual-write loss gap;
- provides concrete practice with transactional consistency/eventual delivery;
- adds an outbox table and dispatcher process/logic.

## Alternatives considered

- publish then save or save then publish without outbox: rejected because either order has an unrecoverable ambiguity window.
- distributed transaction across SQL and broker: unnecessary/unavailable complexity.
