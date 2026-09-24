# ADR-004 — Azure Service Bus for durable processing handoff

**Status:** Accepted

## Context

Feed retrieval should not remain coupled to slower semantic/model processing. Processing must tolerate retries and failures without losing source items or producing duplicates.

## Decision

Use one Azure Service Bus queue named `content-processing` for the initial async boundary. Use the Azure Service Bus emulator locally where supported.

Application-level idempotency remains mandatory even if broker duplicate detection is enabled.

## Consequences

- ingestion can finish independently of analysis;
- worker can retry/scale separately;
- DLQ enables diagnosis;
- introduces eventual consistency and message-contract responsibilities.

## Alternatives considered

- in-process queue: simpler but weak durability/independent scaling.
- Azure Storage Queue: viable simpler option; Service Bus chosen because DLQ, duplicate-detection options and messaging semantics are useful learning/operational features here.
- Kafka/Event Hubs: unjustified streaming complexity.
