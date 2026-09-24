# Slice 3 — RSS ingestion and durable handoff

This document freezes the implementation-level boundaries for Slice 3. It
implements FR-004, FR-005 and FR-006 without beginning semantic analysis,
grouping, relevance, brief generation, manual-article intake, or a user-facing
ingestion control.

## Responsibilities

- The finite .NET Ingestion host loads enabled RSS/Atom Sources, fetches each
  one independently, records an `IngestionRun`, normalizes entries, and applies
  deterministic duplicate detection.
- SQL Server is the source of truth. A new `SourceItem` and its
  `OutboxMessage` are committed in one database transaction; no direct broker
  send happens inside that transaction.
- The dispatcher sends pending outbox records to the single `content-processing`
  Service Bus queue and marks a record dispatched only after a successful send.
- The .NET Processor skeleton receives `SourceItemReady` messages and persists
  an idempotent SQL inbox receipt before acknowledging delivery. The inbox is
  the durable pending-work source for the later intelligence/grouping stage.
  It does not call Python or create updates in this slice.

## Normalization and duplicate boundary

Source entries retain their original source URL, title and external identifier
for traceability. Comparison values are normalized separately:

1. source ID plus external ID when an external ID exists;
2. normalized canonical URL when available;
3. content hash when text is available; and
4. normalized title within a configurable seven-day UTC window when the prior
   keys are unavailable.

The first matching rule treats the entry as already known. A duplicate creates
neither a new `SourceItem` nor an outbox record. The title window defaults to
seven days, matching the approved initial candidate horizon, and is kept in
configuration so it can be calibrated without rewriting domain rules.

Each retrieval sends `If-None-Match` and `If-Modified-Since` when the Source
has stored validators. A `304 Not Modified` is a successful ingestion run with
zero retrieved/new items. One source failure is recorded on that source/run and
does not stop another enabled source. Feed retrieval uses the approved bounded
timeout and public-address outbound policy; raw feed content is never logged.

## Durable message contract

The queue remains `content-processing` (ADR-004). The version-one body is a
small JSON envelope with no article text or credentials:

```json
{
  "messageId": "outbox-guid",
  "sourceItemId": "source-item-guid",
  "sourceId": "source-guid",
  "ingestionRunId": "ingestion-run-guid",
  "correlationId": "correlation-guid",
  "occurredAtUtc": "2026-09-13T00:00:00Z",
  "traceParent": "optional-w3c-traceparent"
}
```

`messageId` is the outbox identifier in canonical GUID form and is also the
broker message ID. Retrying an undispatched outbox record therefore preserves
one stable business/message identity. The Processor treats `sourceItemId` as
the idempotency key; repeated broker delivery must not create another product
object. Correlation and W3C trace context are carried for diagnostics. The
envelope is an internal .NET/Service Bus contract rather than a public REST
API, so its fields are changed atomically with producer and consumer until a
future versioning need exists.

## State and recovery

- New committed items begin `Queued`; the associated outbox row begins pending.
- Dispatch failure leaves the row pending with retry diagnostics. A successful
  send marks only that row dispatched.
- The Processor skeleton writes a `ContentProcessingInbox` receipt with a unique
  `SourceItemId`, validated envelope/correlation, receipt time and pending state
  before completing a normal message. Duplicate delivery reuses that receipt.
  A fresh process can read pending receipts from SQL after the broker message
  has been completed. `SourceItem.ProcessingStatus` remains `Queued`; receiving
  a message is not semantic completion. Slice 5 will consume these pending
  receipts when the analysis/grouping application behavior exists.
- Already terminal source items may be acknowledged without another receipt.
  Invalid messages are dead-lettered. Invalid outbox payloads are quarantined
  with bounded diagnostics rather than repeatedly retried. Transient receive
  failures use the configured broker delivery limit and DLQ. A dispatched
  handoff that expires before it is consumed is dead-lettered rather than
  dropped, so an unconsumed message stays observable and recoverable instead of
  silently reintroducing a delivery-side loss window. Matching production queue
  provisioning (bounded delivery count, dead-letter-on-expiry, TTL) is owned by
  Slice 9.

This is at-least-once delivery by design. It avoids the database-commit /
message-publish loss window without claiming exactly-once messaging.

## Verification boundary

Slice 3 tests use committed RSS/Atom fixtures and controlled HTTP handlers;
they never call live feeds. SQL Server integration coverage verifies the real
migration, ingestion idempotency, and transactional outbox persistence. Service
Bus emulator coverage exercises the actual publisher/receiver and settlement
boundary in isolated disposable containers when Docker is available. Fast
publisher/consumer substitutes cover individual failure decisions as well.

## Review remediation contract

The durable inbox was clarified during independent review after the initial
consumer completed nonterminal messages using only a state read. A database
row left `Queued` without a pending-work receipt or recovery path was not an
adequate durable handoff. The inbox, duplicate receipt rule and fresh-process
recovery test close that gap within the existing .NET/SQL architecture.

Entry failures are isolated within a feed. Runs retain counts for entries
retrieved and committed even when another entry fails. A partial run must not
advance conditional HTTP validators past unrecoverable entries. A finite job
returns failure when it attempted sources but none completed successfully;
an empty source list is a successful no-op. Local cadence remains an external
four-hour invocation of the finite executable; cloud scheduling is Slice 9.
