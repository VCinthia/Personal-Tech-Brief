# Slice 7 — Feedback, save, history, evaluation

## Delivered behavior

Slice 7 adds the user interactions and product-evaluation signals on top of the
immutable briefs from Slice 6: relevance feedback, saved state, a source-open
telemetry signal, a finite brief-history UI, and a read-only evaluation summary.
It implements the contracted subset of FR-013 plus FR-015/016, UC-009/010/011
and AC-012/013/014. It never becomes an infinite feed of the ingested corpus.

## Flow and responsibility

- **Interactions** (`.NET`, own tables — never touching a brief snapshot):
  - Feedback: `PUT /api/v1/updates/{id}/feedback` `{value: relevant|notRelevant}`
    appends a `UserFeedback` row; the "current" feedback is the latest row;
    `DELETE` clears it. Attributable to the update (and optionally the originating
    brief item, cross-validated to belong to that update).
  - Saved: `PUT`/`DELETE /api/v1/updates/{id}/saved` — idempotent save/unsave
    (`SavedUpdate` PK = `TechnologyUpdateId`, single-user).
  - Source-open: `POST /api/v1/updates/{updateId}/sources/{sourceItemId}/open`
    records a `SourceOpenEvent` only when the source actually supports the update.
- **Evaluation** (`GET /api/v1/evaluation/summary`, read-only): deterministic
  FR-016 aggregates — items ingested/selected, grouped duplicates, latest
  relevant/not-relevant tallies, source opens, saves, briefs and items-per-brief,
  and source distribution — computed from persisted data, not engagement scoring.
- **History UI** (Blazor `/history`): pages the Slice-6 history REST (finite) and
  opens a brief to show its originally-selected updates from the immutable
  snapshot; the current and history views carry the feedback/save/open controls.
- `.NET` owns all of this; no Python and no new messaging.

## Key decisions and trade-offs

- **Interactions never mutate the immutable brief.** Feedback/saved/open live in
  separate tables keyed to the `TechnologyUpdate`, with restrict FKs so history
  and live rows survive; an integration test asserts a brief's `GET /briefs/{id}`
  is byte-identical before and after feedback+save+open (the historical-snapshot
  invariant).
- **Latest-supersedes without losing history.** Feedback is append-only; the
  evaluation tally takes the most recent row per update, so "current" feedback is
  derived, not overwritten — retaining the signal history the spec allows.
- **Attribution stays consistent.** An optional `briefItemId` on feedback is
  validated to belong to the same update (not merely to exist), so a mismatched
  id can't create an internally-inconsistent attribution row. (Review low, fixed.)
- **Evaluation is product telemetry, not engagement tracking.** Single-user, no
  per-user profiling; `sourceDistribution`/"selected" counts read the immutable
  `BriefItemSource` snapshot and de-duplicate across briefs so media repetition
  doesn't inflate counts.
- **Scope held.** `Read/Pending` and `Dismiss` (FR-013) were deferred by owner
  decision — no `§12` contract and "UI semantics to be designed"; the value
  parser positively rejects `read`/`dismiss`. No manual-article or cloud work.

## Known limitations and accepted debt

- ~~The UI does not re-hydrate persisted feedback/saved state on load.~~
  **Resolved (Slice 7.1):** the brief read models now surface each item's current
  feedback and saved flag (a read-only live projection that never touches the
  immutable snapshot), and the controls hydrate from it on load. See the Slice 7
  closure note.
- Save is check-then-insert rather than an atomic upsert; two truly-concurrent
  saves of one update could race to a PK conflict (no data loss). Unreachable in
  the single-user MVP.
- The latest-feedback tie-break on an identical `CreatedAtUtc` tick falls to the
  larger `FeedbackId` GUID — deterministic but not temporal; unreachable at
  `datetime2` resolution with real inserts.
- Source-open is validated against the **live** `TechnologyUpdateSource`
  association (per the frozen design); if an update is later regrouped and a link
  removed, opening that source from a historical brief view would 404 even though
  the snapshot still lists it — a traceability-vs-regrouping nuance for a later
  slice.
- `PUT feedback` returns 404 (not 400) for an unknown/mismatched `briefItemId`;
  within the documented `{204,400,404}` set.

## Verification and independent review

Release build zero warnings/errors; 211 .NET unit and 64 integration tests
(disposable real SQL Server: migration; feedback persisted/attributable and
latest-supersedes; save/unsave idempotency; source-open rejected for a
non-supporting source; the evaluation summary counts against seeded data; the
documented status codes via `WebApplicationFactory`; and the immutability
assertion). `dotnet format`, the EF pending-model check, the package
vulnerability scan, and the regenerated public OpenAPI contract test all pass.
The Python service is unchanged this slice.

Two independent reviewers examined the integrated diff — a spec/API/UI/test
reviewer and a persistence/data-integrity/evaluation-query reviewer. Both
approved; the persistence reviewer verified every FR-016 signal against its
SQL/LINQ (no off-by-one/double-count/fanout/incorrect-latest). All findings were
low: the `briefItemId` cross-validation and an a11y grouping nit were fixed and
re-reviewed; the rest (state-hydration, save race, tie-break, live-association
validation, 404-vs-400) are accepted with rationale above. No blocker/high/medium
findings.

## Interview connections

This slice demonstrates REST resource/verb semantics (PUT/DELETE idempotency,
202 for accepted telemetry, ProblemDetails), persistence modelling of append-only
signals with a derived "current" state, restrict-FK protection of an immutable
historical artifact, evaluation/product metrics computed deterministically from
data (distinct from engagement tracking), and a finite history UI that upholds
the anti-infinite-feed product invariant.

**Why keep feedback append-only with a derived "current" instead of updating one
row?** It retains the feedback history the spec allows (useful for later
evaluation/tuning) while still giving an unambiguous current value as the latest
row — and it keeps writes simple and contention-free.

**Why is this "evaluation," not "engagement"?** The summary answers "is the
product effective" (how much is ingested vs shown, how relevant, how grouped) —
product metrics — rather than tracking user behavior for its own sake; it is
single-user and adds no profiling.
