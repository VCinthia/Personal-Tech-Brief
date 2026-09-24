# Accepted delivery checkpoints

This ledger records locally accepted repository deliveries. An annotated Git
tag is the durable checkpoint; it is not evidence of a cloud deployment.

| Delivery | Completion tag | Accepted commit |
| --- | --- | --- |
| Phase 0 | `phase-0-complete` | `465d7db` |
| Slice 1 — Interests | `slice-1-complete` | `c434541c74a5cec660cbc7b2f982b026f564b428` |
| Slice 2 — Sources | `slice-2-complete` | `a3205e61de744ca1c40fbe5f5dddab8a40b11f14` |
| Slice 3 — Ingestion and handoff | `slice-3-complete` | tip of `agent/slice-3-integration` at promotion (see tag) |
| Slice 4 — Intelligence API | `slice-4-complete` | tip of `agent/slice-4-integration` at promotion (see tag) |
| Slice 5 — Grouping and relevance | `slice-5-complete` | tip of `agent/slice-5-integration` at promotion (see tag) |
| Slice 6 — Brief generation | `slice-6-complete` | tip of `agent/slice-6-integration` at promotion (see tag) |
| Slice 7 — Feedback, history, evaluation | `slice-7-complete` | tip of `agent/slice-7-integration` at promotion (see tag) |
| UI redesign — retro-editorial reskin | `ui-redesign-complete` | tip of `claude/tech-hub-ui-redesign-a307a3` at promotion (see tag) |

## Slice 1 closure

Interests was independently reviewed, remediated and re-reviewed before
fast-forward promotion. Closure evidence included 8 unit and 12 integration
tests, with disposable SQL Server coverage. The delivery also made the owner's
repository review correction permanent in root `AGENTS.md` and the related
orchestration/provenance specifications. Repository-local read-only reviewers
are the default; optional global review-skill conflicts do not stop delivery.

## Slice 2 closure

Base: `c434541c74a5cec660cbc7b2f982b026f564b428`.
Accepted candidate: `a3205e61de744ca1c40fbe5f5dddab8a40b11f14`.

- Independent architecture/test and security/operations reviews both approved
  the final candidate; all identified high and medium findings were fixed.
- Integrated Release build had zero warnings/errors; 61 unit and 20 integration
  tests passed, with zero skips and real disposable SQL Server migration tests.
- Locked restore, formatting, OpenAPI baseline, EF pending-model check and
  package vulnerability scan passed.
- Promotion used `git merge --ff-only`; `main` matched the accepted SHA and the
  annotated completion tag was created.
- The four temporary Slice 2 writer/remediation worktrees and their branches
  were removed after confirming clean state and ancestry in `main`. Integration
  branches were retained as delivery history.

## Slice 3 closure

Base: `a3205e61de744ca1c40fbe5f5dddab8a40b11f14` (tag `slice-2-complete`).
Reviewed candidate: `1a9e277` (the last behavioral commit; this closure
documentation delta sits on top and is the promoted tip, tagged
`slice-3-complete`).

- Recovery reconstructed the real worktree state: the integration branch did
  not yet contain the durability or ingestion remediations (they were
  uncommitted work in separate worktrees), so they were integrated and
  completed here rather than reimplemented. The Service Bus emulator tests
  from `agent/slice-3-broker-tests` were merged and adapted to the durable
  inbox contract.
- Independent spec/architecture/test and security/operations reviews both
  approved the final candidate. One high (missing test evidence for per-item
  ingestion failure isolation) and the mediums (finite-job exit-code coverage;
  silent drop of a dispatched handoff on TTL expiry) were fixed and
  re-reviewed. Low findings (poison-entry validator stall by design; poison-loop
  bound and multi-host claim clock-skew under the at-least-once contract) were
  accepted with rationale; production queue provisioning is deferred to Slice 9.
- Integrated Release build had zero warnings/errors; 87 unit and 30 integration
  tests passed with zero skips, including disposable real SQL Server (migration,
  transactional outbox, durable-inbox commit/replay/fresh-process recovery) and
  a real Service Bus emulator (identity across replays, abandon→redelivery,
  malformed→dead-letter).
- Locked restore, formatting, EF pending-model check and the package
  vulnerability scan passed.
- Promotion used `git merge --ff-only`; `main` matched the accepted tip and the
  annotated completion tag was created.
- Temporary Slice 3 writer/remediation worktrees and the broker-tests worktree
  were removed after confirming their content was integrated and contained in
  `main`; the stale Slice 1/Slice 2 integration worktrees were also pruned.
  Integration branches were retained as delivery history.

## Slice 4 closure

Base: tip of `main` after Slice 3 promotion and the governance-doc baseline
commit (`e18ebcc`). Reviewed candidate: `36381f7` (the last behavioral commit;
this closure documentation delta sits on top and is the promoted tip, tagged
`slice-4-complete`).

- The internal Python Intelligence API (`/internal/v1/items/analyze` and
  `/internal/v1/similarity`) and the .NET typed client were implemented in
  parallel from a frozen contract (`docs/architecture/intelligence-api.md`) by
  two delegated writers, then integrated. The expensive `updates/generate`
  endpoint was deliberately deferred to Slice 6.
- Independent spec/architecture/contract/test and security/operations reviews
  both approved the final candidate. Two mediums (a server-side analyze budget
  that made the per-attempt timeout/retry unreachable; a one-sided
  "cross-language" contract test) were fixed and re-reviewed. Low findings
  (attempt-count vs "up to 3", non-https endpoint, no .NET overall retry budget,
  no client-side [0,1] clamp) were fixed or accepted with rationale. No blocker
  or high findings.
- Verification: Python ruff format/lint, mypy (src and tests, strict) and 52
  pytest with no live LLM; .NET Release build zero warnings/errors, 103 unit and
  41 integration tests (including 11 Docker-free cross-language contract tests),
  formatter, EF pending-model check and package vulnerability scan.
- The canonical internal-API JSON fixtures live in `docs/contracts/intelligence/`
  as the single shared source of truth, validated from both stacks.
- Promotion used `git merge --ff-only`; `main` matched the accepted tip and the
  annotated completion tag was created.
- The two temporary Slice 4 writer worktrees and their branches were removed
  after confirming their content was integrated and contained in `main`; the
  integration branch was retained as delivery history.

## Slice 5 closure

Base: tip of `main` after Slice 4 promotion (`8e42693`). Reviewed candidate:
`3419d27` (the last behavioral commit; this closure documentation delta sits on
top and is the promoted tip, tagged `slice-5-complete`).

- Grouping and relevance were implemented on `.NET` from a frozen design
  (`docs/architecture/grouping-and-relevance.md`) by one delegated writer (a
  single tightly-coupled workstream — domain, persistence, scorer, pipeline and
  Processor consumer — so no parallel split was manufactured), then integrated.
- Independent spec/architecture/scoring/test and persistence/concurrency/
  operations reviews both approved the final candidate. The persistence reviewer
  found one high (a dirty EF change tracker on the mid-commit failure path
  defeating the bounded-retry escalation and risking batch cross-contamination)
  and one medium (per-batch rather than per-receipt scope); both were fixed with
  a per-receipt scope plus a change-tracker reset on the failure path and a
  real-SQL regression test, then re-reviewed to approval. Low findings (the
  "strongest interest = greatest weighted contribution" reading of §20, the
  additional-interest bonus rate, the stricter one-update-per-item index, the
  separate-transaction inbox-receipt completion, and contention/query notes)
  were accepted with rationale.
- Verification: Release build zero warnings/errors; 145 unit and 45 integration
  tests (disposable real SQL Server for the migration, create/merge grouping,
  idempotent reprocessing, score recomputation, and failure accounting), with a
  fake intelligence provider — no live LLM; `dotnet format`, EF pending-model
  check, and package vulnerability scan all pass.
- Promotion used `git merge --ff-only`; `main` matched the accepted tip and the
  annotated completion tag was created.
- The temporary Slice 5 implementation worktree and its branch were removed after
  confirming their content was integrated and contained in `main`; the
  integration branch was retained as delivery history.
- Post-tag owner correction: the ambiguous §20 "strongest matched interest"
  reading (a review low that materially affects the ≤5-item brief
  selection/ranking) was escalated to the owner, who chose **priority-first**
  (highest-priority matched interest leads, ties broken by the stronger match).
  Implemented on `agent/slice-5-relevance-fix`, spec/scoring re-reviewed and
  approved, and fast-forwarded onto `main`. The `slice-5-complete` tag remains on
  the first accepted candidate; `main`'s tip is the corrected behavior recorded
  here.

## Slice 6 closure

Base: tip of `main` after Slice 5 (`3ba83d8`). Reviewed candidate: `c59e7e5`
(the last behavioral commit; this closure documentation delta sits on top and is
the promoted tip, tagged `slice-6-complete`).

- Brief generation was built from a frozen design (`docs/architecture/brief-
  generation.md` + the `/internal/v1/updates/generate` contract) by two delegated
  writers in parallel (Python generate endpoint; .NET selection/orchestration/
  REST/UI), then integrated. The generation-failure disposition (exclude the
  candidate, never invent) was an explicit owner decision taken before build.
- Independent spec/selection/API/test and persistence/concurrency/security
  reviews both approved the final candidate. Findings fixed and re-reviewed: a
  high (the generate request could exceed the frozen contract bounds and
  permanently drop a selected candidate → clamp/cap to `GenerateContractLimits`),
  a medium (missing max-length boundary contract test → shared fixture bound on
  both stacks), a medium (an orphaned `Generating` brief shadowing `/current` →
  `/current` is Completed-only + startup reconciliation), a security medium (the
  source link had no scheme allowlist → http/https-only anchor), and a low
  (startup-reconciliation vs concurrent-POST race → cutoff scoped to
  pre-existing briefs). Remaining lows (pagination tie-break, unbounded channel,
  stacked retry budgets, window upper bound) accepted with rationale.
- Verification: Release build zero warnings/errors; 176 .NET unit and 56
  integration tests on disposable real SQL Server (migration, immutable snapshot,
  already-briefed exclusion, empty brief, startup reconciliation with the race
  guard, and the public endpoints); the Python service 67 pytest with the fake
  provider (no live LLM). `dotnet format`, EF pending-model check, package
  vulnerability scan, and Python ruff/mypy all pass. The shared generate contract
  fixtures (incl. max-length) are validated from both stacks.
- Promotion used `git merge --ff-only`; `main` matched the accepted tip and the
  annotated completion tag was created.
- The two temporary Slice 6 writer worktrees and their branches were removed
  after confirming their content was integrated and contained in `main`; the
  integration branch was retained as delivery history.

## Slice 7 closure

Base: tip of `main` after Slice 6 (`c413a20`). Reviewed candidate: `33a0a4f`
(the last behavioral commit; this closure documentation delta sits on top and is
the promoted tip, tagged `slice-7-complete`).

- The contracted FR-013 subset (Relevant/NotRelevant feedback + Save), source-open
  telemetry, the finite brief-history UI, and the FR-016 evaluation summary were
  built by one delegated .NET writer from a frozen design, then integrated.
  Read/Pending and Dismiss were deferred by owner decision (no §12 contract; UI
  semantics undesigned).
- Independent spec/API/UI/test and persistence/data-integrity/evaluation-query
  reviews both approved. The persistence reviewer verified every FR-016 signal
  against its SQL/LINQ (no off-by-one, double-count, join-fanout or
  incorrect-latest). All findings were low; two were fixed and re-reviewed (a
  briefItemId→update cross-validation gap; an a11y grouping nit), and the rest
  (UI state not re-hydrated on load — surfacing is optional per the frozen
  design; non-atomic save under a single-user race; GUID tie-break; source-open
  validated against the live association; 404-vs-400 for a bad briefItemId) were
  accepted with rationale.
- Verification: Release build zero warnings/errors; 211 .NET unit and 64
  integration tests on disposable real SQL Server (migration; feedback
  persisted/attributable + latest-supersedes; save/unsave idempotency;
  source-open association guard; evaluation counts; documented status codes; and
  an assertion that interactions never mutate a Brief/BriefItem snapshot).
  `dotnet format`, EF pending-model check, package vulnerability scan, and the
  regenerated public OpenAPI contract test all pass; the Python service is
  unchanged this slice.
- Promotion used `git merge --ff-only`; `main` matched the accepted tip and the
  annotated completion tag was created.
- The temporary Slice 7 implementation worktree and its branch were removed after
  confirming their content was integrated and contained in `main`; the
  integration branch was retained as delivery history.
- Follow-up (Slice 7.1, owner-requested): the brief read models now surface each
  item's current feedback + saved flag (a read-only live projection; the
  immutable snapshot is untouched — proven by a strengthened immutability test),
  so the interaction UI hydrates persisted state on load instead of rendering
  neutral. The public OpenAPI baseline gained two `BriefItemResponse` fields.
  Independently reviewed (contract fidelity, query correctness with the
  EvaluationService tie-break, immutability) and approved with no findings; 211
  unit and 66 integration tests green. Fast-forwarded onto `main`; the
  `slice-7-complete` tag stays on the first accepted candidate and `main`'s tip is
  this follow-up.

## Slice 8 — Manual article URL intake

- Implements FR-003, UC-004, AC-011: `POST /api/v1/articles` accepts one public
  article URL, fetches it under an SSRF-safe bounded policy, extracts a title +
  main-text excerpt (AngleSharp, parse-only; no raw HTML stored), and persists a
  source-less `ManualUrl` `SourceItem` plus its outbox message in one transaction,
  reusing the durable content-processing handoff. The article's host never becomes
  a permanent source. Two EF migrations: nullable outbox `SourceId`/`IngestionRunId`
  for source-less items, and a filtered unique index on the manual normalized-URL
  key. AngleSharp authorized as decision D-024.
- Independent review used two read-only perspectives (spec/architecture/test;
  security/operations). No blocker/high findings. Two medium findings — concurrent
  duplicate correctness (the duplicate catch was not backed by a constraint) and a
  per-request `HttpClient`/handler leak — were remediated (a filtered unique index
  plus read-committed isolation; a materialized, self-disposing pinned requester)
  and re-reviewed to APPROVE. Low findings were fixed (reserved IP literals → 400;
  a direct nullable-guard unit test) or given explicit dispositions (content-hash
  dedup scope and the lossy migration `Down` accepted; per-request rate limiting
  deferred to Slice 9 operational hardening), recorded in
  `docs/architecture/manual-article-intake.md`.
- Verification: Release build zero warnings/errors; 253 .NET unit and 74
  integration tests on disposable real SQL Server (source-less persistence with
  single-outbox atomicity, resubmit and feed→manual dedup, a concurrent-submission
  race proving exactly one item, inbox acceptance of the source-less envelope, and
  the documented `202/400/422` outcomes). `dotnet format`, the EF
  pending-model-changes check, the package vulnerability scan, and the regenerated
  public OpenAPI contract test all pass; the Python service is unchanged this slice.
- Promotion used `git merge --ff-only`; `main` matched the accepted candidate and
  the annotated `slice-8-complete` tag was created. The docs-only closure delta was
  applied after the re-reviewed candidate and covered by a bounded delta check.
- The temporary Slice 8 work was authored on the integration branch by the
  orchestrator (one tightly-coupled backend workstream plus a thin UI, which did
  not meet the bar for concurrent delegated writers); independent review was still
  performed by non-authoring reviewers. The integration branch is retained as
  delivery history.

## UI redesign closure

Base: `cc2488b` (`main`, tag `slice-8-complete` lineage).
Accepted candidate: the promoted commit tagged `ui-redesign-complete`.

- Scope: presentation-only reskin of the Blazor web UI to the approved
  retro-editorial theme. Confirmed CSS + static-asset only — no `.razor` or
  `.cs` changed — so no runtime behaviour, contract, migration or test outcome
  is affected. `wwwroot/app.css` was rewritten as a tokenised system reusing the
  existing semantic classes; self-hosted `woff2` fonts were added under
  `wwwroot/fonts/` (SIL OFL 1.1, `NOTICE.md`) with no external font CDN.
- Verification (scoped to what the slice touched, per the efficiency layer):
  Release build of the solution — zero warnings, zero errors; `dotnet format
  --verify-no-changes` clean; no secret committed (`.env` gitignored and absent
  from the diff; fonts are genuine OFL `woff2`, not secrets). Unit/integration
  suites were not re-run: no compiled code changed and no code path lets a
  stylesheet alter their outcome — an explicit proportionate disposition, not an
  omission. The theme was verified visually with the real page markup and then
  end-to-end in the running containerised app on every page.
- Independent review (one non-authoring read-only reviewer; the changed surface
  hit none of the cross-service / security-secrets / cloud / persistence /
  concurrency categories that call for a second). Verdict CHANGES-REQUESTED with
  no blocker/high findings. One MEDIUM — the keyboard focus ring at `#F44E1C`
  measured 2.92:1 on the cream ground, below WCAG 2.1 SC 1.4.11's 3:1 for a
  non-text indicator, affecting real primary CTAs — was fixed by moving the ring
  to `#D2400F` (3.88:1 on cream, ≥3:1 on every surface), the reviewer's own
  pre-computed prescription. One LOW — placeholder text at 2.86:1 — was fixed by
  darkening it to the `--muted` token. The post-review delta is CSS-token-only
  and non-behavioural, covered by the reviewer's stated acceptance of that exact
  remediation; the implementation note's AA claim was corrected to match.
- Promotion used `git merge --ff-only`; `main` matched the accepted candidate and
  the annotated `ui-redesign-complete` tag was created. The temporary UI worktree
  work was authored by the orchestrator on `claude/tech-hub-ui-redesign-a307a3`
  (a single tightly-coupled presentation workstream that did not meet the bar for
  concurrent delegated writers); independent review was still performed by a
  non-authoring reviewer.

No cloud account, paid resource or production deployment action was performed
for any slice. No post-MVP feature was started. Later delivery records will
be added only after their own closure gates pass.
