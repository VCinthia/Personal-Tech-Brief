# Slice 6 — Brief generation (frozen design)

This freezes the design for Slice 6. It implements FR-011/012/014/018,
UC-007/008 and AC-006/007/008/009/010/015/016 (pipeline §13 stages 9–11;
calibration §20). `.NET` owns selection, orchestration, persistence, the public
API and the UI; Python performs only the selected-candidate GenAI generation
through a new internal endpoint. Nothing here changes grouping/scoring (Slice 5)
or ingestion.

## Scope

Deliver:

- **Candidate selection** (`.NET`, deterministic): for a brief window, exclude
  candidates below the selection threshold (55, Slice 5 config), rank the rest
  by `CurrentRelevanceScore` (descending; deterministic tie-break by
  `TechnologyUpdate.Id`), take at most **5**, never fill unused slots, and treat
  zero selections as a valid empty brief. Exclude `TechnologyUpdate`s already
  represented in a prior completed brief (the approved simpler rule, §20).
- **Brief window** (§20): start at the previous completed brief's generation
  time; when none exists, look back 7 days. Configurable.
- **Selected-only generation** (Python `POST /internal/v1/updates/generate`):
  for each selected candidate, generate a concise title, a source-grounded
  summary, and a why-relevant explanation, with validated structured output.
- **Immutable snapshot** (`.NET`): persist `Brief` and `BriefItem` per
  `docs/specs/11-data-model.md`; later regrouping/re-analysis must never rewrite
  a historical brief.
- **Public REST** (`/api/v1`): `POST /briefs` (202 Accepted, async),
  `GET /briefs/current`, `GET /briefs?cursor=&limit=`, `GET /briefs/{id}`.
- **UI**: a Blazor page showing the current brief (title, topic, summary,
  why-relevant, date, and source references with links), and an empty-brief
  state. The main experience is never an infinite feed (FR-014/AC-016).
- **Failure/retry**: bounded retry of transient generation failures; the
  disposition below.

Out of scope (do NOT build): feedback / saved / history-beyond-listing
interactions (Slice 7); manual article intake (Slice 8); cloud deployment
(Slice 9). Do not change Slice 5 grouping/scoring behavior.

## Generation flow (async, 202)

`POST /api/v1/briefs` creates a `Brief` in `Generating` and returns `202
Accepted` with the brief id / status location; generation runs asynchronously
and transitions the brief to `Completed` (possibly with zero items) or `Failed`.
`GET /briefs/current` returns the latest brief and its status;
`GET /briefs/{id}` returns a specific brief. Generation is user-triggered
(no scheduling — §20). Only one brief generates at a time is not required, but a
new generation defines its own window from the last **completed** brief.

Per selected candidate, in ranked order:

1. Build the generate request from persisted data only — the candidate's
   supporting `SourceItem`s (title/excerpt/url/published) and its matched
   interests — and call `IIntelligenceApiClient.GenerateAsync`.
2. Validate the structured response; persist a `BriefItem` snapshot (rank,
   title/summary/whyRelevant, source references, score snapshot, and
   generation/model/prompt version metadata).
3. **On persistent generation failure** (after bounded transient retry per §20:
   up to 3 attempts, backoff; 60s per-attempt budget): **exclude that candidate
   from the completed brief** — do not invent fallback facts (§13), do not fill.
   The brief completes with the successfully generated items (fewer than the
   number selected, possibly zero — all valid). The excluded candidate is **not**
   marked as briefed, so it stays eligible for a future brief; record the failure
   for diagnostics. (Owner-chosen disposition among §13's two allowed options.)

A candidate counts as "briefed" (and thus excluded from later briefs) only once
it has a persisted `BriefItem` in a completed brief.

## Cost control (BR-009, AC-015)

Generation is the only expensive GenAI step and runs **only** for candidates
already selected by the deterministic pipeline (below-threshold and
already-briefed candidates never reach generation). Reuse of prior generation
is not required for the MVP; generation metadata (version/model/prompt) is
persisted for later cost estimation.

## Immutability (architecture invariant)

`Brief`/`BriefItem` rows are write-once snapshots. Re-running grouping/scoring
updates the live `TechnologyUpdate` only; existing briefs are never rewritten.
A brief's `BriefItem.RelevanceScoreSnapshot`/title/summary/whyRelevant capture
the values at generation time.

## Source attribution (FR-012/017, AC-010)

Each `BriefItem` retains references to its supporting source items (at least
one), surfaced in the API/UI with a way to open the original source.

## Verification boundary

- `.NET` unit: selection (threshold exclusion, ranking, max-5, no-fill, empty,
  already-briefed exclusion, window) with a fake repository; orchestration with
  a fake `IIntelligenceApiClient` (success, per-item generation failure →
  candidate excluded, transient-retry-then-success, all-fail → empty completed
  brief); the generate typed-client mapping over a stubbed handler; a
  cross-language contract test for the generate DTOs against the shared
  `docs/contracts/intelligence/` fixtures.
- `.NET` integration (real SQL): migration; immutable `Brief`/`BriefItem`
  snapshot; already-briefed exclusion across two briefs; empty brief persists
  with `SelectedCount = 0`; the public endpoints return the documented status
  codes/payloads (202 on create, current/by-id/history). Intelligence is a fake
  provider — no live LLM.
- Python: pytest for the generate endpoint (structured output, validation
  failure → 502, boundary 400 / semantic 422, timeout mapping) with the fake
  provider; ruff/mypy. No live LLM.
- Gates: .NET build/format, EF pending-model check, package vulnerability scan;
  Python ruff/mypy/pytest.
