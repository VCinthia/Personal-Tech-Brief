# Slice 7 — Feedback, save, history, evaluation (frozen design)

Implements FR-013 (the contracted subset), FR-015, FR-016, UC-009/010/011 and
AC-012/013/014. All `.NET`; no Python. It adds per-update user interactions and
product-evaluation signals on top of the immutable briefs from Slice 6, and a
finite history UI. It never exposes an infinite feed of the ingested corpus.

## Scope

Deliver:

- **Feedback**: `Relevant` / `Not relevant` on an update, persisted and
  attributable; latest supersedes for evaluation (history rows may be retained).
- **Saved state**: save / unsave an update, persisted for later review.
- **Source-open signal**: record that the user opened a supporting source
  (evaluation telemetry, not an engagement feed).
- **Brief history UI**: browse previous briefs and their originally-selected
  updates (the history REST already exists from Slice 6); a finite list, not a
  corpus feed.
- **Basic product-evaluation queries**: a read-only summary of the FR-016
  signals.

**Explicitly out of scope (owner decision):** `Read/Pending` and `Dismiss` states
from FR-013 — they have no `§12` contract and FR-013 marks their UI semantics
"to be designed"; deferred. No manual-article intake (Slice 8), no cloud (Slice 9).
Historical briefs stay immutable: feedback/saved/open live in their own tables
keyed to the `TechnologyUpdate`, never mutating a `BriefItem` snapshot.

## Entities (data model §11) + migration

- `UserFeedback` (`Id`, `TechnologyUpdateId`, `BriefItemId` nullable,
  `FeedbackType` Relevant/NotRelevant, `CreatedAtUtc`). Append-only rows; the
  "current" feedback for an update is the most recent row. FK restrict to
  `TechnologyUpdate` (and `BriefItem` when present) so history survives.
- `SavedUpdate` (`TechnologyUpdateId` PK, `SavedAtUtc`). Single-user (no UserId).
  Save is idempotent; unsave deletes the row.
- `SourceOpenEvent` (`Id`, `TechnologyUpdateId`, `SourceItemId`, `OpenedAtUtc`).
  Append-only telemetry.

One EF Core migration `AddInteractionsAndEvaluation` (LF/no-BOM; no pending model
changes). FKs restrict (do not cascade from live `TechnologyUpdate`/`SourceItem`).

## REST (public `/api/v1`, ProblemDetails, §12)

- `PUT /updates/{id}/feedback` body `{ "value": "relevant" | "notRelevant" }` —
  records feedback for the update (optionally carrying the originating
  `briefItemId`); returns `204`. `404` if the update does not exist; `400` on a
  bad value.
- `DELETE /updates/{id}/feedback` — clears the update's current feedback
  (records/represents "no current feedback"); `204`.
- `PUT /updates/{id}/saved` — idempotent save; `204`.
- `DELETE /updates/{id}/saved` — unsave; `204` (idempotent — deleting an absent
  save is still `204`).
- `POST /updates/{updateId}/sources/{sourceItemId}/open` — records a
  `SourceOpenEvent`; `404` unless `sourceItemId` is a supporting source of
  `updateId` (validated against `TechnologyUpdateSource`); `202`/`204` otherwise.
- `GET /evaluation/summary` — read-only FR-016 aggregates (below).

The brief read/history endpoints (`/briefs`, `/briefs/current`, `/briefs/{id}`,
`/briefs?cursor=&limit=`) already exist (Slice 6) and are reused by the UI.
Feedback/saved state is exposed on the update as it appears in a brief (the brief
read models may surface the current feedback + saved flag for display).

## Evaluation summary (FR-016)

`GET /evaluation/summary` returns deterministic counts computed from persisted
data (no engagement scoring):

- `itemsIngested` — total `SourceItem`s;
- `itemsSelected` — total `BriefItem`s across completed briefs;
- `groupedDuplicates` — supporting `TechnologyUpdateSource` links beyond the
  first per `TechnologyUpdate` (i.e. duplicates folded into an update);
- `relevantFeedback` / `notRelevantFeedback` — counts of updates whose latest
  feedback is Relevant / Not relevant;
- `sourceOpens` — total `SourceOpenEvent`s;
- `saves` — total `SavedUpdate`s;
- `briefCount` and `itemsPerBrief` (e.g. average selected per completed brief);
- `sourceDistribution` — selected/ingested item counts grouped by `Source`.

This is product-effectiveness telemetry; it is not user-facing engagement
tracking and adds no per-user profiling (single-user MVP).

## History UI

A Blazor `/history` page lists previous briefs (paged via the Slice-6 history
REST), and opening one shows the updates originally selected for it (from the
immutable snapshot). The current-brief and history views expose the feedback /
save / open controls per update. The experience is a finite list of briefs and
their selected items — never a browseable feed of every ingested item (FR-015/
AC-014 constraint).

## Verification boundary

- Unit: feedback latest-supersede + value validation; saved idempotency; the
  source-open association guard; the evaluation aggregation logic (each signal)
  with a fake repository.
- Integration (real SQL): migration; feedback persisted/attributable and latest
  supersedes; save/unsave idempotent; source-open rejected for a non-supporting
  source; the evaluation summary counts against seeded data; the endpoints return
  the documented status codes via `WebApplicationFactory`; historical briefs are
  never mutated by interactions.
- Gates: .NET build/format, EF pending-model check, package vulnerability scan;
  the public OpenAPI document updated for the new endpoints and its contract test
  green.
