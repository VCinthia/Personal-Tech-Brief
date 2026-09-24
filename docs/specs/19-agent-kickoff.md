# Personal Tech Brief — Agent Kickoff

**Status:** Ready for first implementation session

This is the exact starting instruction for the primary agent.

## Initial instruction to the primary agent

```text
You are the primary technical integrator/orchestrator for the Personal Tech Brief using Spec-Driven Development.

Before changing code, use targeted context rather than recursively loading the whole documentation tree. Read in this order:
1. AGENTS.md
2. docs/specs/README.md
3. docs/specs/00-product-definition.md
4. docs/specs/09-agent-working-agreement.md
5. docs/specs/10-system-design.md
6. docs/specs/14-testing-strategy.md
7. docs/specs/15-security-observability.md
8. docs/specs/16-local-cloud-environments.md
9. docs/specs/17-implementation-plan.md — Phase 0 only
10. docs/specs/19-agent-kickoff.md
11. docs/specs/20-calibration-and-operational-defaults.md
12. docs/specs/21-agent-orchestration-and-worktrees.md
13. docs/specs/22-environment-isolation-and-tool-provenance.md
14. only the ADRs referenced by those documents/Phase 0 decisions.

Load other FR/NFR/BR/UC/AC/spec documents only when a Phase 0 deliverable or conflict requires them. Do not make every delegated agent reread this list; subagents follow minimum-sufficient context from AGENTS.md.

The approved product/specification documents are authoritative. Do not reinterpret the product as a generic news feed and do not add deferred features.

Start ONLY with Phase 0 — Repository bootstrap from docs/specs/17-implementation-plan.md.

You are responsible for decomposition, integration and final verification. **This kickoff requires deliberate multi-agent execution when subagent/worktree capabilities are available.** Follow AGENTS.md, docs/specs/21-agent-orchestration-and-worktrees.md and docs/specs/22-environment-isolation-and-tool-provenance.md.

Do not manufacture unsafe parallelism, but do not use that as a reason to keep all work in the primary agent. First freeze repository layout and shared root conventions in the primary worktree. Then, if subagent/worktree capabilities are available, **you MUST delegate at least these two independent write workstreams**:

1. `.NET bootstrap agent` — owns only the agreed .NET source/test paths.
2. `Python bootstrap agent` — owns only the agreed Python source/test paths.

Run them in separate Git worktrees/branches created from an explicit integration/base commit. The primary agent retains ownership of shared/root files, solution integration, Docker/compose integration and final verification **on the Phase 0 integration branch, never directly on `main`**. After integration, **you MUST use at least one independent review/validation subagent that did not author the implementation** to inspect the integrated repository for spec/acceptance compliance, architecture boundaries, test quality, scope leakage and accidental tooling leakage. Use one independent reviewer by default for Phase 0. Add a second reviewer only if the integrated diff materially changes a high-risk surface that justifies a security/operations perspective; do not add reviewers merely to increase agent count.

If the environment does not expose subagent/worktree capability **or cannot provide an independent reviewer**, STOP before implementation and report that limitation rather than silently performing a single-agent/self-reviewed Phase 0.

Before implementation:
- perform the environment/tool provenance preflight from AGENTS.md and `22-environment-isolation-and-tool-provenance.md`;
- capture `git status --short`, `git branch --show-current`, `git rev-parse HEAD` and `git worktree list`;
- treat `main` as protected: if currently on `main`, create a dedicated `agent/phase-0-integration` branch/worktree from the clean baseline **before any file modification**;
- never author Phase 0 implementation commits directly on `main`;
- explicitly list any visible non-project skills/instructions/tools and confirm that unrelated project-specific tooling will not be used;
- identify the Phase 0 exit criteria and any applicable specification identifiers;
- summarize the Phase 0 deliverables you will create;
- decompose Phase 0 into dependency-aware tasks;
- identify which tasks, if any, are safe to delegate and why;
- identify shared files/contracts that must remain under primary-agent ownership;
- list any genuine specification conflict that would block Phase 0;
- do not ask product questions already answered in the specs;
- if no blocking conflict exists, proceed.

Phase 0 must establish:
- .NET 10 solution and the project structure defined by the specs;
- ASP.NET Core bootstrap/health skeleton without product feature implementation;
- Python 3.14 FastAPI project and locked tooling;
- test projects and executable test commands;
- formatting/lint/static-type commands;
- local SQL Server infrastructure;
- Azure Service Bus emulator if it works reliably in the development environment; otherwise document the exact blocker without silently substituting a different broker;
- health endpoints/checks;
- local developer startup workflow;
- CI skeleton;
- repository README;
- canonical docs/ADR placement;
- no Slice 1 product behavior.

For every delegated task, provide bounded ownership, applicable identifiers, allowed paths/contracts, required checks and expected handoff evidence. Delegated agents may not change specs, architecture or shared contracts independently.

Integrate delegated work yourself. After integration, run all checks introduced by Phase 0 from the integrated repository. Passing isolated worktrees are not enough. Then run the mandatory independent review gate. Assign blocker/high findings back to an implementation owner, integrate the fixes, rerun checks and require reviewer re-validation before Phase 0 may be reported complete.

Fix failures caused by your changes. Do not proceed to Slice 1.

At the end, report one coherent Phase 0 result containing:
- final repository structure/files created;
- base commit SHA, Phase 0 integration branch and current branch;
- `git worktree list` plus task/delegation/worktree summary (including work intentionally kept sequential);
- explicit confirmation that `main` was not used for implementation and remained unchanged;
- commands to run locally;
- checks executed from the integrated repository and their results;
- requirement/exit-criteria evidence;
- implementation decisions/assumptions made within the approved baseline;
- blockers or environmental limitations;
- anything intentionally deferred to Slice 1;
- disposition of temporary branches/worktrees;
- independent reviewer(s), findings, remediation status and final reviewer verdict.

Do not merge/promote the reviewed Phase 0 integration branch to `main` in this run. Leave it ready for promotion.

STOP after Phase 0 and wait for explicit instruction before promotion or Slice 1.
```

## After Phase 0

Review Phase 0 output, then give the primary agent one slice at a time from `17-implementation-plan.md`.

Recommended instruction form:

```text
Implement Slice N from docs/specs/17-implementation-plan.md as the primary technical integrator.

First read the referenced FR/NFR/BR/UC/AC identifiers and relevant architecture/contracts/ADRs. Produce a concise dependency-aware task plan mapping code and tests to those identifiers.

Follow AGENTS.md and docs/specs/21-agent-orchestration-and-worktrees.md. Treat `main` as protected: create a dedicated slice integration branch/worktree before any modification and do not promote it to `main` without a separate explicit instruction. For non-trivial code slices, the primary agent acts mainly as orchestrator/integrator: delegate bounded implementation work when subagents are available, and delegation is mandatory when at least two independent workstreams exist. Independent post-integration review by a non-authoring agent is mandatory. Freeze shared contracts before parallel dependent implementations. Concurrent writing agents must use separate worktrees/branches. Parallelize implementation, not decisions. Never use unrelated project/employer skills or tooling.

Implement the slice end to end, integrate delegated work, then run all relevant checks from the integrated repository and report evidence against the slice exit/acceptance criteria.

Do not start any later slice. Do not add deferred product features. If implementation exposes a real spec/architecture/shared-contract conflict, pause the affected path, document it and surface the decision rather than changing product behavior silently.
```

## Human review checkpoint

Before requesting the next slice, review:

- whether you understand the generated code path;
- one meaningful design trade-off in the slice;
- one failure path and how it is handled;
- tests and what they actually guarantee;
- any concurrency/worktree integration decision that occurred.

The objective is not merely a finished repository; it is to be able to explain and defend its design and implementation decisions.
