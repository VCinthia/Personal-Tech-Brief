# Slice 5 — Grouping and relevance

## Delivered behavior

Slice 5 turns the durable per-item handoff (Slice 3 inbox) into a small set of
scored candidate `TechnologyUpdate`s. A Processor consumer drains pending inbox
receipts and, for each source item, calls the Python Intelligence API (Slice 4)
to analyze it, groups it with existing updates by deterministic similarity,
records the supporting-source and interest-match associations, and computes a
deterministic relevance score. This implements FR-007/008/009/010/017 and
BR-004/009/010/011 (pipeline §13 stages 6–8; calibration §20). It produces no
brief — prioritization here means computing and storing the score that later
brief selection (Slice 6) will rank by.

## Flow and responsibility

`inbox receipt → analyze (Python) → bounded similarity grouping → relevance score → persist → receipt processed`

- **Analyze** (`.NET` calls `IIntelligenceApiClient.AnalyzeAsync`): topics,
  per-interest match strengths, and an impact signal. Deterministic business
  rules never depend on the model (BR-010): grouping uses the returned
  similarity number and the .NET scorer is a pure function.
- **Group** (`GroupingPipelineService` + `IIntelligenceApiClient.ComputeSimilarityAsync`):
  candidates are bounded by a recent window, topic overlap, and a maximum
  comparison count; the best match ≥ the merge threshold (0.78) links to that
  `TechnologyUpdate`, otherwise a new one is created. Equality merges; false
  merges are treated as worse than missed merges.
- **Score** (`RelevanceScorer`, pure/deterministic): the calibration §20 model —
  strongest interest (60/40/20 × clamped strength) + capped bonus, recency,
  impact, and independent-source support — returning the total and an
  explainable per-component breakdown.
- **Persist** (`SqlGroupingRepository`): link/create, association, interest-match
  upsert, score, and `SourceItem.ProcessingStatus = Processed` in one
  serializable transaction; the inbox receipt is then marked processed.
- `.NET` owns every business decision; Python only supplies stateless signals
  (ADR-002). Nothing here connects Python to the database.

## Key decisions and trade-offs

- **Idempotency under at-least-once delivery.** The unique
  `(TechnologyUpdateId, SourceItemId)` pair plus a per-item unique index make a
  redelivery a no-op: an already-associated or terminal item early-exits, a
  concurrent duplicate create rolls back its orphan, and the unique-violation is
  caught and resolved to the winning update. The durable "handled" markers are
  the association + `SourceItem.Processed` (inside the grouping transaction); the
  inbox receipt is completed in a separate write and only drives retry, so a
  crash between the two is re-derived as "already handled" on the next poll.
- **Failure accounting must survive a dirty change tracker.** A grouping commit
  can call `SourceItem.MarkProcessed()` in memory and then fail on the second
  `SaveChanges` (deadlock/timeout), rolling back in the database but leaving the
  entity tracked as `Processed`. The failure path clears the change tracker
  before recording the failure so it reads committed state and actually
  increments the bounded-retry `FailureCount`; each receipt is also processed in
  its own scope/DbContext so one item's staged entities cannot contaminate
  another's transaction. (Both were independent-review findings, fixed and
  re-reviewed.)
- **"Strongest matched interest" = priority-first (owner decision).** §20's
  "take the strongest matched active interest" is ambiguous. The owner chose the
  priority-first reading: the highest-priority matched interest leads (High >
  Medium > Low, ties broken by the stronger match), scored as its priority
  weight × its strength, so a high-priority interest the user configured is
  never outranked by a lower-priority interest that merely matches more
  strongly. Because this drives the ≤5-item brief selection/ranking (core
  product output), the ambiguity was escalated rather than defaulted, and the
  priority ordering is independent of the (configurable) weights so it holds
  even if two weights are configured equal.
- **Deterministic, configurable weights.** All scorer weights, the 55 threshold,
  the 0.78 merge threshold, and the grouping window/comparison bounds are
  validated configuration, not scattered constants.

## Known limitations and accepted debt

- The inbox receipt is completed in a separate transaction after the grouping
  commit (the frozen design said "same transaction where feasible"); the split
  is replay-safe as above, but not a single atomic write.
- Under heavy contention, concurrent merges into the same update can raise
  transient deadlocks that are retried (extra retries, no incorrectness).
- The distinct-supporting-host signal loads supporting rows and extracts hosts
  client-side; bounded in practice for MVP volumes.
- The additional-interest bonus rate (5 × strength per extra match, capped at 10)
  is an implementation choice within §20's "capped at 10" bound.
- The Processor requires `IntelligenceApi:BaseAddress` (and the grouping/relevance
  options) at start; real runs must point it at the deployed Python service —
  there is no Python service in `compose.yaml` yet (local/cloud wiring is Slice 9).

## Verification and independent review

Release build zero warnings/errors; 145 unit tests (scorer boundary behavior on
every component and the 55 threshold; grouping merge/create/equality decisions
with a fake intelligence client and fake repository) and 45 integration tests on
disposable real SQL Server (migration; new-item create; cross-host merge with
score recomputation; idempotent reprocessing; and the failure-accounting
regression). `dotnet format`, the EF pending-model check, and the package
vulnerability scan pass; Python is a fake provider so no live LLM is used.

Two independent reviewers examined the integrated diff: a spec/architecture/
scoring/test reviewer (approved — §20 fidelity, merge behavior, scope discipline)
and a persistence/concurrency/operations reviewer. The latter found one high
(a dirty change tracker on the mid-commit failure path defeating bounded-retry
escalation and risking batch cross-contamination) and one medium (per-batch
rather than per-receipt scope); both were fixed with a per-receipt scope plus a
change-tracker reset and a real-SQL regression test, then re-reviewed to
approval. Low findings were accepted with rationale. No blocker findings.

## Interview connections

This slice demonstrates application/domain design for grouping and scoring,
deterministic algorithms kept independent of the LLM, cross-service integration
through a typed client, EF Core modelling of a many-to-many with a unique pair,
serializable-transaction idempotency under at-least-once delivery, the subtlety
of EF change-tracking across a failed transaction, and background-service
consumers with per-message scoping.

**Why keep scoring deterministic and out of the model?** So relevance is
explainable and testable (BR-010/011): the score decomposes into interest,
recency, impact, and source-support components with fixed configurable weights,
and the model only contributes semantic signals (topics/matches/impact) that
feed those deterministic rules.

**Why complete the inbox receipt in a separate transaction?** The durable
"handled" state is the source-item association plus `Processed`, committed
atomically; the receipt merely drives retry. Splitting the receipt completion
keeps the grouping transaction focused and stays replay-safe because a crash
before completion is re-derived as already-handled on the next poll.
