# Slice 5 — Grouping and relevance (frozen design)

This freezes the design for Slice 5. It implements FR-007/008/009/010/017 and
BR-004/009/010/011 (processing-pipeline §13 stages 6–8; calibration §20). All
business behavior below is taken from the approved specs — nothing here is a new
product decision. `.NET` owns every business decision; Python provides only the
analyze and similarity signals through the Slice 4 `IIntelligenceApiClient`.

## Scope

Deliver, in `.NET`:

- three persisted entities — `TechnologyUpdate`, `TechnologyUpdateSource`
  (many-to-many with a unique `(TechnologyUpdateId, SourceItemId)` pair and a
  nullable `SimilarityScore`), and `UpdateInterestMatch` — exactly per
  `docs/specs/11-data-model.md`, with an EF Core migration;
- a **deterministic** relevance scorer implementing the calibration §20 model,
  returning both the numeric total and an explainable per-component breakdown;
- a grouping/analysis application service that, for a pending inbox receipt,
  calls Python analyze, bounds the candidate set, calls Python similarity, links
  or creates a `TechnologyUpdate`, records interest matches and source
  association, and stores the relevance score;
- a Processor consumer that drains pending `ContentProcessingInbox` receipts
  (Slice 3) and runs the pipeline, marking each receipt processed idempotently;
- unit tests (scorer boundary behavior, grouping merge/create decisions with a
  fake intelligence client and fake repository) and real-SQL integration tests
  (migration, idempotent reprocessing, unique-pair constraint, grouping query).

**Out of scope (do NOT build):** brief generation / selection / final GenAI
presentation (`updates/generate`, `Brief`, `BriefItem`) — Slices 6; feedback /
saved / history (Slice 7); manual article intake (Slice 8); any Web/UI change.
Prioritization (FR-010) here means computing and storing the relevance score
that later brief selection will rank by — no brief is produced in this slice.

## Pipeline (per pending inbox receipt)

Owner: .NET Processor consumer + application service; Python via the typed client.

1. Load a bounded batch of pending receipts (`IContentProcessingInboxStore.LoadPendingAsync`).
   The consumer is at-least-once; every step below must be idempotent.
2. Resolve the `SourceItem` and its `Source`. If the item is already in a
   terminal processing state (Processed/Filtered/FailedTerminal) or already
   associated with a `TechnologyUpdate`, complete the receipt without creating a
   duplicate (idempotent replay).
3. **Analyze** (stage 6): call `IIntelligenceApiClient.AnalyzeAsync` with the
   item's title/excerpt/content and the configured active interests as candidate
   interests. Receive normalized keywords/event descriptors, topics,
   per-interest match strengths, and the impact signal. Analyze results may be
   reused by source-item id + analyzer version (cost control, §13) — a simple
   "analyze once per item" guard is sufficient for the MVP.
4. **Group** (stage 7): bound candidates by a recent time window, topic overlap,
   and a maximum comparison count (all configuration). Build a candidate-text
   representation, call `IIntelligenceApiClient.SimilarityAsync` against the
   bounded representatives, take the best match; if its similarity ≥ the merge
   threshold (**0.78**, configuration) link the source item to that
   `TechnologyUpdate`, otherwise create a new one. Equality qualifies for merge.
   False merges are worse than missed merges — keep the threshold conservative.
5. **Score** (stage 8): compute the relevance score deterministically (below),
   using the group's newest supporting source timestamp and its distinct
   supporting-host count. Store `CurrentRelevanceScore` on the `TechnologyUpdate`
   and the `UpdateInterestMatch` rows.
6. Mark the receipt processed and set `SourceItem.ProcessingStatus = Processed`
   in the same transaction as the association where feasible, so a redelivery is
   a no-op. Transient dependency failures (Python/SQL) abandon for bounded broker
   retry; validation/domain failures are terminal (no infinite retry).

## Relevance score (calibration §20 — deterministic, configurable weights)

- **Interest component:** the strongest matched active interest, read
  **priority-first** — pick the highest-priority matched interest (High > Medium
  > Low, ties broken by the stronger match), then score its priority weight
  (High `60`, Medium `40`, Low `20`) × its match strength (clamped `[0,1]`). A
  high-priority interest the user configured therefore leads even when a
  lower-priority interest matches more strongly. Every other matched interest
  adds a capped bonus up to `10` points total. (Owner decision on the ambiguous
  §20 wording "take the strongest matched active interest".)
- **Recency component** (newest supporting source timestamp): ≤24h `+20`;
  >24h–≤72h `+12`; >72h–≤7d `+5`; older `+0`.
- **Impact component** (from analyze): High `+20`; Medium `+10`; Low/Unknown `+0`.
- **Independent-source support** (distinct supporting hosts): 1 `+0`; 2 `+4`;
  3+ `+7`.
- **Selection threshold:** `55` points (configuration; not persisted per item as
  a flag in this slice — selection happens at brief time). The score is not
  user-facing; the range is internal.

The scorer returns the total plus a component breakdown (name, points, short
explanation) for diagnostics/logging (BR-011's user-facing explanation text is
generated later at brief time; this slice provides the structured basis). All
weights/threshold are configuration, not hardcoded constants scattered across
layers.

## Idempotency and durability

- The unique `(TechnologyUpdateId, SourceItemId)` constraint plus a
  check-before-insert make re-linking a no-op. Creating a `TechnologyUpdate`
  for an already-associated source item must not duplicate it.
- Marking the inbox receipt processed (add a terminal `Processed` status to
  `ContentProcessingInboxStatus` and a completion method on the inbox store) and
  advancing `SourceItem.ProcessingStatus` are the durable "handled" markers;
  `LoadPendingAsync` already filters to `Pending`.
- Historical data is never silently rewritten; re-analysis updates the live
  `TechnologyUpdate` only (briefs are immutable snapshots in later slices).

## Source attribution (FR-017)

Every `TechnologyUpdate` retains at least one `TechnologyUpdateSource`, and a
grouped update retains all supporting source references. Grouping never discards
a source association.

## Verification boundary

- Unit: scorer boundary tests (each component's thresholds; the 55 boundary;
  bonus cap; host-count buckets; clamp), grouping decisions (merge at ≥0.78,
  create below, equality merges) with a fake `IIntelligenceApiClient` and a fake
  repository. No live Python, no DB.
- Integration (real disposable SQL Server, per §14): the migration applies; a
  new item creates one `TechnologyUpdate` + one association + interest matches;
  a similar item merges (unique pair holds, second association added, score
  recomputed); reprocessing the same receipt is a no-op (idempotency); the
  candidate-finding query behaves under the window/topic bounds. Python is a fake
  provider — no live LLM.
- Ruff/mypy are unaffected (no Python change in this slice). .NET build/format,
  EF pending-model check, and the package vulnerability scan apply.
