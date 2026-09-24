# Slice 3 — Ingestion and handoff

## Delivered behavior

Slice 3 adds the first content pipeline: a finite .NET Ingestion executable
reads enabled RSS/Atom Sources, fetches each independently, records an
`IngestionRun`, normalizes entries, applies deterministic duplicate detection,
and commits each new `SourceItem` together with one transactional
`OutboxMessage` in a single database transaction. A separate Processor host
dispatches pending outbox records to the single `content-processing` Service
Bus queue and consumes them into a durable, idempotent SQL inbox. This
implements FR-004, FR-005 and FR-006 without starting semantic analysis,
grouping, relevance, brief generation or any Python call — those remain later
slices.

The product invariant holds: ingestion may take in far more than is ever
displayed, deduplication discards known entries, and nothing here creates a
user-facing feed.

## Flow and responsibility

`ingest → transactional outbox → dispatch → Service Bus → processor inbox`

- **Ingestion** (`PersonalTechBrief.Ingestion`): a finite job. It loads
  enabled sources, retrieves each with a bounded, conditionally-requested,
  SSRF-guarded HTTP call, parses RSS/Atom, and persists new items. One source
  failure is recorded on that run and does not stop other sources; one bad feed
  entry is isolated and does not abort its source. The process exits non-zero
  only when it attempted sources but none completed; an empty enabled-source
  list is a successful no-op.
- **Transactional outbox**: `SqlSourceItemPersistenceService` commits the new
  `SourceItem` and its `OutboxMessage` in one serializable transaction. No
  broker send happens inside that transaction, closing the commit/publish loss
  window without claiming exactly-once.
- **Dispatch** (`OutboxDispatcher` / `OutboxDispatcherWorker`): claims pending
  rows with a SQL update-lease, publishes the version-one envelope, and marks a
  row dispatched only after a successful send. Invalid payloads are
  **quarantined** with bounded diagnostics instead of retried forever.
- **Processor inbox** (`SourceItemReadyMessageHandler` +
  `SqlContentProcessingInboxStore`): validates the envelope, then commits a
  unique `ContentProcessingInbox` receipt before completing the broker message.
  The receipt — not the broker — is the durable pending-work source a later
  intelligence slice will consume. `.NET` owns all state; Python does not
  participate.

## Key decisions and trade-offs

- **Durable inbox over a state-read acknowledgement.** An earlier consumer
  completed messages after only reading `SourceItem.ProcessingStatus`, leaving a
  row `Queued` with no pending-work record and no recovery path once the broker
  message was gone. The remediation commits a durable receipt keyed uniquely by
  `SourceItemId` before completion, so a fresh process recovers pending work
  from SQL alone. Duplicate delivery reuses the receipt; the unique key is the
  real idempotency backstop.
- **Serialize the idempotency decision, don't hope for a race-free window.**
  `AcceptAsync` opens a transaction and takes an `UPDLOCK, HOLDLOCK` on the
  `SourceItems` row for the message before inspecting/inserting the receipt, so
  two concurrent deliveries of the same item cannot both insert. The unique PK
  still backstops it.
- **Broker dispositions map to explicit outcomes.** Missing item or reference/
  state mismatch → dead-letter (poison, never retried); inbox faults → abandon
  for bounded broker retry; accepted / already-accepted / terminal → complete.
  This avoids both message loss and infinite poison loops.
- **Quarantine invalid outbox payloads.** A payload that cannot form a valid
  envelope is moved to a terminal `Quarantined` status with a safe diagnostic
  rather than retried indefinitely, keeping the dispatcher loop live.
- **Per-item ingestion isolation with honest counts.** A failed entry increments
  a failure count and the run is failed with retrieved/new counts retained, but
  conditional validators (ETag/Last-Modified) are **not** advanced past
  unrecoverable entries, so a later cycle can re-retrieve and retry them.
  Committed entries stay safe through deterministic deduplication.
- **Title-only entries use the bounded title window, not a permanent hash.**
  Entries without excerpt text produce no content hash, so dedup falls back to
  the seven-day title window instead of pinning a weak permanent key.

## Verification and independent review

The integrated candidate passed a Release build with zero warnings/errors, 87
unit and 30 integration tests with no skips, locked restore, formatter, EF
pending-model check and the package vulnerability scan. Integration coverage
uses disposable real SQL Server (migration, transactional outbox, dedup, and
the durable inbox commit/replay/fresh-process recovery) and a real Service Bus
emulator (identity preserved across replays, abandon→redelivery with an
incremented delivery count, malformed→dead-letter with a diagnostic reason).

Two independent reviewers examined the integrated diff: a spec/architecture/test
reviewer and a security/operations reviewer — the persistence/concurrency and
messaging-durability surface is exactly the category that warrants two
perspectives. The security/operations reviewer approved and confirmed the inbox
lock plus unique-PK backstop is race-safe and deadlock-free, the quarantine
state machine is terminal, dead-letter/abandon decisions are sound, SQL is
parameterized, and no secrets or untrusted content reach logs or instruction
paths. It raised one medium durability finding — a dispatched handoff could be
silently dropped on broker TTL expiry — which was fixed by enabling
dead-letter-on-expiry for the `content-processing` queue. The
spec/architecture/test reviewer initially requested changes for missing test
evidence on the per-item ingestion failure-isolation path (high) and the
finite-job exit-code rule (medium); both were closed with targeted tests and
re-reviewed to approval. No blocker or high findings remained.

## Known limitations and accepted debt

- Production Service Bus queue provisioning — bounded `MaxDeliveryCount`,
  dead-letter-on-expiry, and TTL matching the local emulator config — is owned
  by Slice 9 (cloud scheduling/provisioning). The poison-message loop bound and
  the multi-host outbox-claim clock-skew window are acceptable under the
  documented at-least-once contract and depend on that provisioning.
- A deterministically failing feed entry keeps its source's run `Failed` and
  holds back conditional-validator advancement indefinitely, by design (a
  partial run must not skip past unrecoverable entries). The only current signal
  is a warning log; richer observability or per-entry quarantine is deferred to
  a later slice.
- Local ingestion cadence is an external four-hour invocation of the finite
  executable; cloud scheduling is Slice 9.

## Interview connections

This slice demonstrates the transactional outbox pattern, at-least-once
messaging with an idempotent SQL inbox, dead-letter vs abandon settlement
decisions, SQL update-lease claiming and `UPDLOCK/HOLDLOCK` serialization,
EF Core migrations for a new durable table, .NET background/finite hosts,
`CancellationToken` propagation, and Testcontainers-style integration testing
against real SQL Server and a real Service Bus emulator.

**Why a durable inbox instead of acknowledging on a state read?** Because
completing a broker message after only reading state leaves no durable record
of pending work: if the process dies after completion, the item is neither in
the queue nor in a pending-work store. A committed receipt makes the handoff
recoverable from the database alone.

**Why quarantine rather than dead-letter a bad outbox payload?** The outbox is
producer-side state, not a broker message; there is no queue entry to
dead-letter yet. Marking the row terminal with a bounded diagnostic stops an
infinite dispatch-retry loop while preserving the record for operational
inspection.
