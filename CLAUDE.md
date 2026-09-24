# CLAUDE.md — Execution efficiency layer for Personal Tech Brief

**Status:** Operational aid, not a specification. It governs *how* Claude Code
executes already-approved work; it does not define *what* the product does.

## 0. Precedence — read this first

This file is subordinate to the authority hierarchy defined in
`docs/specs/README.md`. It never overrides `AGENTS.md` or any document in
`docs/specs/`. Its only job is to remove repeated, evidence-free work from an
execution that must otherwise keep the exact rigor already required by
`AGENTS.md`, `docs/specs/09-agent-working-agreement.md` and
`docs/specs/21-agent-orchestration-and-worktrees.md`.

If anything below ever reads as easier or laxer than those documents, the
documents win and this file is wrong — stop and flag it instead of following
this file.

Required reading order for a new session, in this order, and no further
unless a genuine ambiguity remains after reading them:

1. `AGENTS.md` (root) — execution/orchestration rules, protected `main`,
   worktree rules, review gate, promotion protocol, stop conditions.
2. `docs/specs/README.md` — authority hierarchy and document index.
3. `docs/specs/17-implementation-plan.md` — approved phase/slice order and
   what each slice must deliver.
4. The documents for the **active slice only**: its FR/NFR/BR/UC/AC entries,
   its architecture note under `docs/architecture/` if one exists, and its
   `docs/implementation/*.md` implementation note if one already exists.
5. ADRs under `docs/specs/adr/` — only the ones a cited decision actually
   touches. Do not re-read all nine ADRs "to be safe."

Do not re-read `docs/specs/00` through `16` and `18-22` in full at the start
of every session. They do not change between sessions; re-reading the whole
corpus every time a session starts is exactly the redundant work this file
exists to remove. Use `rg` for a targeted lookup instead of opening a file you
already read earlier in the same delivery.

### Promotion to `main` is standing-authorized — do it, don't wait to be told

`AGENTS.md`, `docs/specs/09-agent-working-agreement.md` and
`docs/specs/21-agent-orchestration-and-worktrees.md` §6 now agree: once a
phase/slice's closure gate passes, promotion is standing-authorized. **Do not
wait for the maintainer to say "promote it" — run the merge and create the tag
yourself, the moment the gate passes**, exactly like every other closure-gate
step.

- The gate itself is still real: independent review passed, blocker/high
  findings remediated and re-reviewed, medium/low findings dispositioned,
  the full suite (§8) green on the exact candidate. Do not skip the gate to
  promote faster — the gate is the thing that makes promotion safe to do
  unattended.
- Once the gate passes: run `git merge --ff-only` into `main`, create the
  annotated completion tag, clean up temporary worktrees per `AGENTS.md`'s
  promotion protocol, and report what you did (final SHA, tag, reviewer
  verdict, worktree state) — all in the same pass, without a pause for
  confirmation in between.
- The maintainer can still interject at any point, including mid-promotion, if
  something unrelated occurs to them or they notice something in the moment.
  That is an ordinary interruption like any other, not a gate you wait for.
  Absent an interjection, proceed on your own.
- This standing authorization does not extend to production deployment, paid
  resources, external account mutations, or destructive operations with
  uncertain state — those still require their explicit approval (§23).

(An earlier version of `CLAUDE.md` required a separate per-promotion
chat instruction, mirroring an intermediate state of `AGENTS.md`. The maintainer
reviewed that and confirmed they do not want to be a required gate for
routine promotion — this section reflects that decision.)

## 1. Session bootstrap — do this before any mutable action

Run a short recovery, then act on what you actually find, not on the table in
§8 below (that table is a snapshot from the session that produced this file
and will go stale):

```text
git status --short
git branch --show-current
git rev-parse HEAD
git tag -l
git worktree list
git log --oneline --graph --all -30
```

For every worktree `git worktree list` shows, check inside it:

```text
git -C <worktree-path> log --oneline -5
git -C <worktree-path> status --short
git -C <worktree-path> diff --stat
```

Do not create a new branch or worktree before this recovery tells you whether
one that would serve the same purpose already exists. Reuse it if it does.

## 2. The one question that governs every process decision

Before adding a step — another full test run, another reviewer, another
worktree, another re-read of a spec — ask:

> Does this produce evidence that does not already exist for this exact code,
> or does it reduce a real, named risk?

If the honest answer is "no, we already generated this evidence for this
commit" — skip it. If the answer is "yes" — do it, fully, without shortcuts.
This file only removes the "no" cases; every "yes" case keeps the full rigor
`AGENTS.md` already requires.

## 3. Slices stay the unit of delivery

Keep the vertical-slice structure in `docs/specs/17-implementation-plan.md`
exactly as is. Do not merge conceptually distinct slices to save process
overhead, and do not let a slice grow scope beyond what
`17-implementation-plan.md` assigns it (see `AGENTS.md` §Scope). The
optimization in this file happens **inside** a slice's workflow, not by
collapsing slice boundaries.

## 4. Review staffing

Default: **one** independent, fresh, read-only reviewer per slice, exactly as
`docs/specs/21-agent-orchestration-and-worktrees.md` §11 already specifies.
The reviewer:

- has not authored the code it reviews;
- reads root `AGENTS.md`, the integrated diff, and only the specs/ADRs/
  acceptance criteria the diff actually touches;
- classifies findings `blocker`/`high`/`medium`/`low` with evidence;
- does not modify code.

Add a **second**, differently-focused reviewer only when the changed surface
concretely includes one of the categories `AGENTS.md` and
`docs/specs/21-agent-orchestration-and-worktrees.md` §11 already name:
cross-service surfaces, security/secrets, cloud/infrastructure,
persistence/migrations, or substantial concurrency. Slice 2 (Sources) is the
repository's own precedent: it used a spec/architecture/test reviewer **and**
a security/operations reviewer because feed validation touches SSRF and DNS
rebinding — see `docs/implementation/slice-2-sources.md`. Slice 3 (SQL
outbox/inbox persistence, migrations, and concurrent message
processing/retry/DLQ) falls squarely in the persistence-and-concurrency
category both documents name, so it is a reasonable default candidate for two
reviewers as well — this is not a new rule, just naming which existing
category Slice 3 already sits in. If you add a second reviewer, write one
sentence in the handoff naming which of these categories justified it.

Never spin up two reviewers "for balance" or "to be thorough" without a named
risk — that is the duplicated-review-without-reason pattern this file exists
to remove.

## 5. Finding triage

- **Blocker** — fix before anything else proceeds.
- **High** — fix before promotion.
- **Medium** — evaluate technically. Fix now if it threatens correctness,
  security, durability, a public contract, an acceptance criterion, or
  material maintainability — or if the fix is small, local and low-risk
  regardless. Otherwise, accept explicitly as documented debt (see §9's
  Definition of Done) with a one-line rationale.
- **Low** — document and move on by default; fix only if trivial and it does
  not open a new chain of unrelated work.

This matches `AGENTS.md`'s closure-gate requirement that every medium/low
finding get an explicit disposition (fixed / accepted with rationale /
deferred to named scope) — it just gives you the triage rule for reaching
that disposition without turning every medium into a new project phase.

## 6. Re-review is a delta review

After remediation, the reviewer checks three things only: the cited findings
are actually fixed, the delta does not introduce an obvious new regression,
and the tests relevant to the fix pass. It does not re-audit the whole slice
from zero unless the remediation materially changed the architecture or risk
surface of the slice (e.g. a durability fix that changes the persistence
model, not a null-check).

Avoid this loop:

```text
implement → review → remediate → FULL re-review → new unrelated finding →
remediate → FULL re-review → ...
```

Depth stays; the audit does not restart from scratch each cycle.

## 7. Testing during implementation: targeted first

While implementing or remediating, run the specific test(s) tied to the
change first — a test class, a test project, an integration fixture, a
migration test, a Python module's tests. Iterate:

```text
change → targeted test → fix → targeted test
```

Do not run the full solution/suite after every small edit. Reserve the full
suite for the points in §8.

## 8. One full suite per integrated candidate

When a slice's implementation is integrated on its integration branch and
believed complete, run the full validation once, scoped to what the slice
actually touched. The exact commands already exist in this repository's
README — reuse them instead of inventing a new checklist:

**.NET** (from the repository root):

```powershell
dotnet tool restore
dotnet restore src/dotnet/PersonalTechBrief.sln --locked-mode
dotnet build src/dotnet/PersonalTechBrief.sln --configuration Release --no-restore
dotnet test src/dotnet/PersonalTechBrief.sln --configuration Release --no-build
dotnet format src/dotnet/PersonalTechBrief.sln --verify-no-changes --no-restore
```

Plus, when the slice touched persistence: an EF Core pending-model check, and
the package vulnerability scan the earlier slices' closure evidence already
used (see `docs/implementation/delivery-checkpoints.md`).

**Python** (from `src/python/intelligence-service`):

```powershell
py -3.14 -m uv sync --locked --all-groups
py -3.14 -m uv run ruff format --check .
py -3.14 -m uv run ruff check .
py -3.14 -m uv run mypy src tests
py -3.14 -m uv run pytest
```

**Infrastructure**, when the slice touched it: `docker compose` config
validation and any container/integration tests the slice's acceptance
criteria require (see `docs/specs/14-testing-strategy.md` for which
integration coverage must use real SQL Server / real Service Bus emulator
rather than a double).

Do not run checks for a stack the slice did not touch, except gates
`AGENTS.md`/`docs/specs/14-testing-strategy.md` name as always-required
(no secret committed; build/tests green; formatting clean).

## 9. After remediation, don't re-run the whole matrix every commit

Sequence after a review finding:

1. implement the fix;
2. run the targeted test(s) for that fix (§7);
3. delta re-review (§6);
4. run the full suite (§8) again only once, against the final candidate —
   not after every intermediate remediation commit.

## 10. Don't re-run the full suite after a no-op promotion

If the exact candidate SHA already passed the full suite, review is approved,
and promotion to `main` is a plain `git merge --ff-only` that changes no
content — do not re-run the full matrix afterward. Verify instead:

- `main` now points at the accepted SHA (`git rev-parse main`);
- working tree is clean;
- the annotated completion tag exists and points at that SHA
  (`git cat-file -p <tag>` — the repo's own tags are all annotated, see
  `.git/packed-refs` and `docs/implementation/delivery-checkpoints.md`);
- required ancestry holds (`git merge-base --is-ancestor <base> main`).

Only re-run tests post-promotion if a merge/rebase actually changed content,
configuration changed, the SHA differs from what was reviewed, or a specific
repository rule demands it.

## 11. Keep real-infrastructure tests where they earn their keep

Do not remove Testcontainers-style real SQL Server or real Service Bus
emulator coverage to save time. `docs/specs/14-testing-strategy.md` and the
Slice 1/2 closure evidence are explicit that SQLite/fakes cannot prove
migration behavior, filtered-index/collation semantics, or real broker
settlement — keep real infra tests for exactly those questions, and for any
explicit acceptance criterion that names them.

Do not add a *new* layer of real infrastructure testing just because it
"would be nice to have" when equivalent evidence already exists, no finding
requires it, and no acceptance criterion names it.

## 12. Worktrees and delegation only where they earn their keep

`AGENTS.md` and `docs/specs/21-agent-orchestration-and-worktrees.md` already
require delegation when a slice has genuinely independent workstreams, and
already forbid manufacturing fake parallelism or spawning agents "to satisfy
an agent count." This file adds no new permission to skip delegation when a
real independent workstream exists — it only reinforces the existing ban on
worktrees/subagents that don't earn their keep:

- Do not create a worktree per role (writer, reviewer, integration, backend,
  frontend) by default "for every slice." Create one when there is real
  concurrent writing, true remediation isolation, or an independent-review
  need.
- Never spawn a subagent solely to re-read specs the orchestrator has already
  read.
- Before creating a new worktree, check §1's recovery output — reuse an
  existing worktree/branch that already serves the purpose instead of adding
  another one.

**This repository currently has worktree sprawl that is itself the pattern
to stop repeating**, not a model to copy: seven worktrees exist for Slice 3
alone (`rss-ingestion`, `persistence-outbox`, `messaging-processor`,
`integration`, `durability-remediation`, `ingestion-remediation`,
`broker-tests`), plus two stale integration worktrees left over from the
already-promoted Slice 1 and Slice 2 (`...-slice-1-integration`,
`...-slice-2-integration`). `AGENTS.md`'s own promotion protocol says to
"remove clean temporary worktrees" after promotion — those two were not
removed. As part of picking up Slice 3 (§14), prune what is safely
integrated or abandoned, and do not let Slice 3 exit with more open
worktrees than the two-writer-plus-reviewer default Phase 0/Slice 1/2 used.

## 13. Freeze contracts before real parallel writers

When two writers genuinely run in parallel, freeze first: DTOs/contracts,
file/component ownership, expected behavior, shared composition points. Then
integrate and resolve only genuine conflicts — this is already `AGENTS.md`'s
"parallelize implementation, not decisions" rule; nothing new here.

## 14. Minimum sufficient context

Do not re-read the full `docs/specs` corpus for every writer/reviewer.
`AGENTS.md` already requires this; the practical habit that makes it true:
use `rg <term> docs/specs` for a targeted lookup before opening a whole file,
and pass a delegated agent identifiers and paths (per the packet format in
`docs/specs/21-agent-orchestration-and-worktrees.md` §5) rather than pasted
spec text.

## 15. Accepted ADRs are not up for relitigating

If `docs/specs/adr/` already accepted a decision (`.NET`/ASP.NET Core, Azure
SQL, Service Bus, Container Apps, the GenAI provider abstraction, Blazor,
transactional outbox, auth/observability approach), implement it. Reopen a
technology/architecture debate only when implementation demonstrably
contradicts the specification — not because a different technology is
generally preferable.

## 16. Security stays non-negotiable, scoped to what's actually present

Keep the controls `AGENTS.md`/`docs/specs/15-security-observability.md`
require wherever the surface is actually present in the slice: SSRF and DNS
rebinding on any outbound fetch, XML DTD/external-entity protection on any
feed/XML parsing, no secrets committed or logged, SQL constraints matching
domain invariants, message durability/idempotency/DLQ for anything
broker-facing, cloud metadata-address blocking (`168.63.129.16` is already
denied per `docs/implementation/slice-2-sources.md`), and no untrusted
content (feed/article text) treated as instructions.

Evaluate these where the slice's actual surface touches them — do not run a
generic OWASP-category checklist against a slice that has no network-facing
or credential-handling surface at all.

## 17. Learning documentation is part of each slice's Definition of Done

Do not defer this to the end of the MVP. Before a slice is considered closed,
`docs/implementation/` must have a note for it, in the exact structure the
two existing notes already use — follow
`docs/implementation/slice-1-interests.md` and
`docs/implementation/slice-2-sources.md` as the template:

```markdown
# Slice N — <name>

## What was built
## Flow and ownership
## Important decisions and trade-offs
## Testing and engineering notes
```

Keep it concise but real: capabilities delivered, main classes/components,
persisted data, endpoints/workers, the actual end-to-end flow (e.g.
`ingest → transactional outbox → dispatch → Service Bus → processor inbox`),
the patterns worth documenting (dependency inversion,
transactional outbox, idempotency, retries/DLQ, health-check liveness vs
readiness split, ProblemDetails, EF migrations, background services,
Testcontainers-style integration testing) — only the ones actually used —
and a short "known limitations / accepted debt" list carried over from §5.
Add two or three Q&A only where they add real value (the
existing notes already model this: "why Testcontainers in addition to
SQLite," "why a durable inbox instead of acknowledging on state alone").

Also update, as part of the same slice:

- `docs/implementation/README.md` — add the new row to the implementation-note table;
- `docs/implementation/delivery-checkpoints.md` — add the slice's row (tag,
  accepted commit) once it is actually promoted, following the exact format
  the Phase 0/Slice 1/Slice 2 rows already use.

This is incremental, high-value documentation — not an academic report. If
writing it is starting to block implementation time for hours, it is too
long.

## 18. Keep the README operationally correct as you go

When a slice adds a variable, local secret, migration, worker, container or
command, update the root `README.md`'s instructions in the same slice — not
at the end. The README already grows this way (Phase 0 → Slice 1 → Slice 2 →
Slice 3 each added their own runnable sections); keep doing that rather than
letting it drift from what the code actually requires.

## 19. Compact handoff state

Maintain a short state block you can drop into a summary at any point,
so work survives context compaction, a usage limit, or a model change without
reconstructing the whole history:

```text
CURRENT SLICE:
CURRENT SHA:
STATUS:
DONE:
ACTIVE WORKTREES:
OPEN BLOCKER/HIGH:
ACCEPTED MEDIUM/LOW:
TEST EVIDENCE:
NEXT:
```

## 20. Slice 3 — continue, do not restart

Do not re-implement Slice 3 from zero. Real work already exists. As of the
session that produced this file, `git worktree list` showed the following —
**verify every line yourself with §1's recovery before acting on it**, this
table is a starting hypothesis, not ground truth:

| Worktree / branch | Base | Last known state |
| --- | --- | --- |
| `agent/slice-3-integration` | `main` (`a3205e61`, tag `slice-2-complete`) | Most advanced candidate. Contains persistence+outbox, RSS ingestion, Service Bus dispatch, processor skeleton, a durability fix (SQL inbox / idempotent receipt pattern), and per-item failure isolation during ingestion — see `docs/architecture/ingestion-and-outbox.md` on this branch, especially its "Review remediation contract" section. Last commit observed: `docs: record delivery evidence and slice 3 recovery contract`. |
| `agent/slice-3-durability-remediation` | `agent/slice-3-integration` @ the pre-fix commit (before the durability fix above was written) | Branch created but **no commit** was made on it; the working tree had uncommitted/staged changes. Diff it against current `agent/slice-3-integration` first — the durability gap it was meant to close may already be closed there, making this worktree redundant, or it may target a different, still-open durability concern. Decide keep/port/discard based on that diff, not from scratch. |
| `agent/slice-3-ingestion-remediation` | same pre-fix base | Same situation: branch created, uncommitted/staged changes present, no commit. Diff against `agent/slice-3-integration`'s per-item isolation fix before redoing anything. |
| `agent/slice-3-broker-tests` | `agent/slice-3-integration` @ the same pre-fix commit | Has one real commit not yet merged into `agent/slice-3-integration`: a test exercising the real Service Bus emulator handoff. Likely still valuable — evaluate and merge it into the integration branch rather than re-writing an equivalent test. |
| `...-slice-1-integration`, `...-slice-2-integration` | — | Stale integration worktrees from already-promoted, already-tagged slices. Prune once you've confirmed nothing unmerged lives in them (§12). |

A prior session reported roughly 81 unit and 24 integration tests passing on
the integrated candidate before the findings above were opened, and Release
build green. Treat that as unverified history, not current fact — confirm the
real current numbers yourself by running §8 once you've reconciled the
worktrees above, rather than trusting the remembered count.

`docs/implementation/slice-2-sources.md` and
`docs/implementation/delivery-checkpoints.md` currently exist only on
`agent/slice-3-integration`, not on `main` — Slice 2's implementation note was
written late, during Slice 3 work, and never separately merged back. It will
land on `main` naturally when Slice 3's integration branch is promoted; no
separate action is needed unless Slice 3 promotion is delayed a long time, in
which case consider a documentation-only bounded delta merge per `AGENTS.md`
closure-gate item 7.

Concrete next steps, in order: (1) run §1's recovery against all five Slice 3
worktrees; (2) diff the two remediation worktrees against the current
integration tip to find out what, if anything, they still add; (3) merge or
discard them accordingly, with a one-line rationale either way; (4) merge
`broker-tests`' commit if it still applies cleanly; (5) confirm which
findings from the prior review remain open against the *current* integration
tip, not against the pre-fix commit; (6) close the remaining gaps using the
targeted-first workflow (§7); (7) run the one full suite (§8); (8) get the
single independent review (§4) — a second reviewer only if the residual
surface still concretely includes a risk category from §4; (9) write
`docs/implementation/slice-3-*.md` (§17) and update the checkpoint ledger;
(10) promote per §0 — merge, tag, clean up worktrees, and report the result —
without waiting for a separate go-ahead.

## 21. Definition of Done for a slice

Unchanged from `AGENTS.md`'s closure gate — restated here only so it's next
to the efficiency rules above, not to replace it:

- every acceptance criterion assigned to the slice has observable
  implementation/test evidence;
- the relevant build is clean and relevant tests pass (§8);
- migrations/contracts touched by the slice are validated;
- blocker/high findings are resolved;
- medium/low findings have an explicit disposition (§5);
- the independent reviewer approved, or clearly documented an accepted
  residual (§4/§6);
- README is operationally accurate for what the slice added (§18);
- the slice's implementation note, the implementation README table, and the
  delivery-checkpoints ledger are updated (§17);
- the compact handoff state (§19) reflects the closed slice;
- promotion follows §0/§10 and `AGENTS.md`'s promotion protocol.

Then continue immediately to the next approved slice — do not pause for
unrequested validation, and do not expand into the next slice's scope early.

## 22. End of the MVP only

Once every MVP slice (through Slice 9 in `docs/specs/17-implementation-plan.md`)
is done, and only then: run one final full-repository validation, an
end-to-end sanity pass, a consolidated architecture overview, a final index
of `docs/implementation/`, the final `main` SHA and all tags, consolidated
test/evidence references, remaining accepted debt, any pending external/cloud
action, and confirmation that no post-MVP scope was started. This final pass
consolidates the incremental slice documentation — it does not replace or
duplicate it.

## 23. Stop conditions

Stop and ask the maintainer, beyond what `AGENTS.md` already lists, only for:

- a real contradiction between authoritative documents;
- a genuinely unspecified product/UI decision;
- independent reviewer capability being entirely unavailable;
- any external/cloud action that could spend money or mutate an external
  account/resource;
- a missing secret/credential only the maintainer can supply;
- a destructive operation against existing data or resources.

Do **not** stop for: promoting to `main` once the closure gate has passed
(§0 — do it, don't ask); an optional global skill conflicting with repo-local
tooling (use the repository-local fallback per `AGENTS.md`
§Environment isolation); a preference for a different architecture than an
already-accepted ADR; a medium/low finding that §5 lets you triage; the
absence of an extra validation nobody required; cosmetic documentation
polish; the option to add more tests without a named risk; or a decision
`docs/specs/07-decision-log.md`/an ADR already settled.
