# Slice 6 — Brief generation

## Delivered behavior

Slice 6 turns scored `TechnologyUpdate`s (Slice 5) into a finite, user-visible
brief: at most five primary updates, possibly zero, never padded. `.NET` selects
candidates deterministically, calls the Python Intelligence API to generate the
presentation text for the selected few, persists an immutable snapshot, and
exposes it over REST and a Blazor page. This implements FR-011/012/014/018,
UC-007/008 and AC-006/007/008/009/010/015/016 (pipeline §13 stages 9–11,
calibration §20).

## Flow and responsibility

`select (window, >=55, rank, <=5, exclude briefed) → generate selected (Python) → immutable Brief/BriefItem snapshot`

- **Selection** (`BriefCandidateSelector`, deterministic, `.NET`): over the brief
  window (previous completed brief's `GeneratedAtUtc`, else now−7d), keep updates
  scoring ≥ 55 (Slice 5's configured threshold) that are not already in a prior
  completed brief, rank by `CurrentRelevanceScore` desc then `Id`, take ≤5, never
  pad, zero is valid. This is the whole product invariant (FR-011/AC-006/007/016).
- **Generation** (Python `POST /internal/v1/updates/generate`, ADR-006): only the
  selected candidates get a concise title, source-grounded summary and
  why-relevant explanation, as Pydantic-validated structured output; the fake
  provider is the default so CI needs no live LLM (BR-009/AC-015 cost control).
- **Snapshot** (`.NET`): each item is persisted as an immutable `BriefItem`
  (title/summary/whyRelevant/score/version metadata + supporting-source
  references); re-running grouping/scoring never rewrites a historical brief.
- **Async 202**: `POST /api/v1/briefs` creates `Brief(Generating)`, enqueues it on
  an in-process channel, and returns 202; a background service drains serially
  (each brief in its own scope), completing it. `GET /briefs/current`,
  `/briefs/{id}`, `/briefs?cursor=&limit=` and a Blazor page read it.

## Key decisions and trade-offs

- **Exclude on persistent generation failure (owner decision).** Transient
  generation failures are retried (bounded); if a selected candidate still can't
  be generated, it is excluded from the completed brief — never a fabricated
  fallback (§13 "must not invent facts") and never padded. The brief completes
  with the successful items (possibly fewer, possibly zero), and the excluded
  candidate is not marked briefed, so it stays eligible for a later brief.
- **Interest reading is settled upstream (Slice 5):** priority-first selection of
  the interest component; brief selection here just ranks by the resulting score.
- **Clamp the generate request to the frozen contract bounds.** The persisted
  domain maxima (source title 800, topic 256, interest name 200) exceed the
  frozen internal-API bounds (512/200/100). Building the request verbatim would
  make the Python endpoint 400-reject a legitimately selected candidate, which
  the orchestrator would then drop forever (never marked briefed). The request
  builder clamps/caps every bounded field/collection to the contract maxima
  (single source of truth in `GenerateContractLimits`); the persisted snapshot
  still keeps the full untruncated sources for attribution. (Independent-review
  high, fixed.)
- **`/current` is the latest Completed brief, with startup reconciliation.** A
  brief stuck in `Generating` (host crash mid-run, or process death between the
  POST commit and the in-process enqueue) must not shadow the last good brief, so
  `GetCurrentAsync` returns the latest Completed one and the background service
  fails pre-existing `Generating` briefs at startup — scoped to briefs created
  before the reconcile cutoff so a POST racing startup is not swept. (Review
  medium + a follow-up low, both fixed.)
- **Source links are scheme-allowlisted.** Snapshot URLs originate in untrusted
  feed item links, so the UI renders a clickable link only for an absolute
  http/https URI, else plain text — closing a stored-XSS vector. (Review medium.)

## Known limitations and accepted debt

- Brief-generation hand-off is an in-process channel: a brief created but not yet
  generated when the process dies is recovered (failed) on the next startup, not
  resumed. The channel is unbounded (only user POSTs enqueue; single-user MVP).
- History pagination orders by `GeneratedAtUtc` only (no Guid tie-break, since
  Guid `<` is not SQL-translatable); two briefs sharing an identical tick at a
  page boundary could skip a row — unreachable at single-user cadence.
- Transient-retry budgets stack (orchestration × client × Python provider), all
  bounded by the 60s per-attempt generate timeout; worst-case latency per
  candidate is large but finite.
- The candidate window upper bound is not enforced at generation time; the
  already-briefed exclusion makes it harmless.

## Verification and independent review

Release build zero warnings/errors; 176 .NET unit and 56 integration tests
(disposable real SQL Server: migration, immutable snapshot, already-briefed
exclusion across briefs, empty brief `SelectedCount=0`, startup reconciliation
with the race guard, and the public endpoints via `WebApplicationFactory`); the
Python service 67 pytest (generate endpoint with the fake provider, structured-
output validation → 502, boundary 400/422, timeout 504, and the shared
generate/max-length contract fixtures round-tripped by both stacks). `dotnet
format`, the EF pending-model check, the package vulnerability scan, and Python
ruff/mypy all pass. No live LLM anywhere.

Two independent reviewers examined the integrated diff — a spec/selection/
API/contract/test reviewer and a persistence/concurrency/security reviewer. The
spec reviewer raised one high (the generate request could exceed the frozen
contract bounds and permanently drop a selected candidate) plus a medium
(missing max-length boundary contract test); the persistence reviewer raised one
medium (an orphaned `Generating` brief shadowing `/current`) and a source-link
scheme gap that was elevated to a security medium, plus a startup-race low. All
were fixed with targeted changes and regression tests and re-reviewed to
approval; the remaining low findings are accepted with rationale above. No
blocker findings.

## Interview connections

This slice demonstrates business-rule ownership (deterministic selection kept in
`.NET`, generation isolated in Python), the async request/acknowledge (202)
pattern with a background worker and per-message scoping, immutable snapshotting
of a historical artifact, structured GenAI generation with validated output and
a fake provider for CI, cross-language contract testing bound to shared fixtures
(including max-length boundaries), and cost control by generating only the
already-selected few.

**Why generate only the selected candidates?** Generation is the one expensive
GenAI step; deterministic selection (threshold, ranking, ≤5, already-briefed
exclusion) removes everything else first, so a run makes at most five generate
calls regardless of how much was ingested — the product processes far more than
it displays without ever becoming an infinite feed.

**Why exclude a candidate whose generation fails instead of a fallback?** The
spec forbids inventing facts; a source-grounded fallback was the alternative, but
excluding keeps the brief's content uniformly model-generated, respects
no-fill/empty, and leaves the candidate eligible for a later brief once the
provider recovers.
