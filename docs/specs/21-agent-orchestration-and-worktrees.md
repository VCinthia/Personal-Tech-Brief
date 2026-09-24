# Personal Tech Brief — Agent Orchestration & Worktree Protocol

**Status:** Approved operational baseline

This document defines how the primary agent may use multiple agents, delegation and Git worktrees while preserving Spec-Driven Development traceability.

## 1. Purpose

Multi-agent execution exists to shorten implementation time without fragmenting product or architecture decisions.

The governing principle is:

> **Parallelize implementation, not decisions.**

Agents and worktrees are controlled execution mechanisms. They are **required when supported and when at least two implementation workstreams are genuinely independent**. They remain inappropriate for tightly coupled/shared-contract decisions.

## 2. Roles

### Primary agent / technical integrator

The primary agent owns the requested phase or slice end to end as orchestrator/integrator. It:

- reads authoritative specs;
- maps the slice to FR/NFR/BR/UC/AC identifiers;
- decomposes work into dependency-aware tasks;
- identifies shared contracts;
- identifies all safely delegable implementation work and delegates it when supported;
- assigns bounded task ownership;
- integrates delegated results;
- runs combined verification;
- reports final evidence.

The primary agent remains accountable for correctness even when implementation is delegated.

### Delegated agent

A delegated agent owns only the explicitly assigned task. It may make ordinary local implementation decisions consistent with existing architecture, but it must not independently:

- change product intent or business rules;
- add deferred scope;
- redefine a shared API/event/data contract;
- create a new material architecture pattern or infrastructure dependency;
- start later slices;
- change specs merely to match its implementation.

A delegated agent may surface a required decision to the primary agent.

## 3. Task decomposition

Use:

```text
Phase
  └─ Slice
      ├─ Task A
      ├─ Task B
      └─ Task C
```

A task is a good delegation unit when it has:

- a specific observable outcome;
- clear input/output contracts;
- limited file/component ownership;
- known dependencies;
- specific tests/checks;
- traceability to the slice acceptance criteria.

Avoid broad ownership such as “backend”, “frontend” or “Python”.

## 4. Dependency graph before delegation

Before spawning parallel work, the primary agent must determine task dependencies.

Examples:

```text
Contract/schema task
        ↓
 ┌──────┴──────┐
.NET consumer  Python provider
 └──────┬──────┘
    Integration tests
```

The contract/schema step is completed or explicitly frozen before the two implementations proceed in parallel.

Tasks sharing migrations, project files, root CI/configuration or the same implementation hotspot should normally remain sequential.

## 5. Delegation packet

Every delegated task should receive enough context to execute without rediscovering product intent.

### Minimum-sufficient context

The primary agent is the only participant expected to hold broad phase/slice context. To control token use, a delegated agent must read root `AGENTS.md` and only the canonical documents/contracts named in its packet. It must not recursively load all specifications unless its task genuinely spans them.

A reviewer should start from the integrated diff and the acceptance/specification/testing/security documents relevant to the changed surfaces. It should expand context only when a finding requires it.

Do not duplicate long spec text in delegation prompts. Pass identifiers and repository paths. Do not spawn a new agent to repeat analysis already captured in a trustworthy handoff.

Required packet:

```text
Task:
Slice:
Implements:
Accepted by:
Base/integration point:
Owned paths/components:
Read-only shared contracts:
Required checks:
Out of scope:
Expected handoff:
```

The primary agent should reference canonical documents rather than copying large sections and risking drift.

## 6. Protected-main and worktree protocol

`main` is a protected stable baseline/promotion branch. Development does not happen directly on `main`.

For each non-trivial phase/slice, the primary agent creates a dedicated integration branch from a recorded `main` base commit, for example:

```text
main
 └─ agent/phase-0-integration
      ├─ agent/phase-0-dotnet
      └─ agent/phase-0-python
```

Shared/root integration work occurs on the integration branch (preferably its own worktree). Concurrent writers use separate task worktrees/branches. The integrated result is reviewed on the integration branch. Promotion to `main` is standing-authorized once the quality gate passes — the orchestrator promotes without waiting for a separate user instruction (see `AGENTS.md` "Phase closure and promotion protocol"). The owner may still interject at any time with something unrelated; that is an ordinary interruption, not a required gate.

At session preflight record:

```text
git status --short
git branch --show-current
git rev-parse HEAD
git worktree list
```

If the current branch is `main`, switch/create the integration branch/worktree **before editing files**.

Use separate worktrees/branches for concurrent write tasks when supported by the environment.

Recommended branch naming:

```text
agent/<slice-id>-<short-task>
```

Recommended worktree naming may mirror the branch.

Rules:

1. No phase/slice implementation commits are authored directly on `main`.
2. One concurrent implementation task owns one worktree/branch.
3. Never assign two writing agents to the same working tree.
4. Each worktree begins from an explicit integration/base commit.
5. Keep changes within assigned ownership unless coordination is requested.
6. Do not mix opportunistic refactors with the delegated task.
7. Do not delete, reset, rewrite or overwrite another task's work.
8. Avoid force operations and history rewriting.
9. Before handoff, incorporate required upstream contract changes and rerun checks.
10. Do not independently merge to the integration branch unless the primary agent explicitly delegates integration authority.
11. Do not merge/promote the integration branch to `main` in the implementation run unless explicitly instructed after review.
12. Delete/clean worktrees only after their work is safely integrated or intentionally abandoned.

Worktrees are not mandatory for sequential tasks.

## 7. Contract-first concurrency

The following are shared contracts and must be coordinated before parallel implementations depend on them:

- public REST endpoints/schemas;
- internal .NET↔Python API schemas;
- Service Bus message/envelope schemas;
- database ownership boundaries and migrations that affect multiple tasks;
- stable identifiers/idempotency semantics;
- cross-component configuration names;
- acceptance behavior observable across component boundaries.

If a delegated task discovers that a contract must change, it reports the proposed change and waits for the primary-agent coordination point rather than silently diverging.

## 8. Specification writes

Implementation agents treat approved specs as read-only unless the requested task explicitly includes documentation/specification work.

When implementation reveals a real conflict:

1. cite the affected documents/identifiers;
2. describe the constraint;
3. propose options/trade-offs;
4. pause the affected implementation path;
5. continue unrelated work only if doing so cannot prejudge the decision.

## 9. Handoff contract

A delegated handoff must include:

```text
Task completed:
Branch/worktree/commit:
Changed paths:
Requirements/ACs addressed:
Checks executed:
Results:
Implementation decisions:
Known risks or limitations:
Integration notes:
```

A handoff with failing checks must identify whether failure is caused by the task, an upstream dependency or the environment.

## 10. Integration protocol

The primary agent integrates in dependency order.

For each delegated result:

1. inspect diff and scope;
2. verify contract compatibility;
3. integrate/reconcile conflicts against authoritative specs;
4. run component checks;
5. after all tasks are integrated, run combined slice checks;
6. execute integration/contract/E2E coverage required by the slice;
7. verify acceptance criteria;
8. report evidence.

Independent green worktrees are insufficient evidence until the integrated repository is green.

## 11. Mandatory independent review

Independent review is a required quality gate for every non-trivial code phase/slice when subagent/reviewer capability is available. It is not optional and must not be replaced by the primary agent reviewing its own work.

The reviewer must not have authored the code under review. Review the **integrated repository/diff**, not only isolated worktrees.

A review task inspects:

- spec/acceptance compliance;
- boundary violations;
- unrequested scope;
- missing failure paths;
- test quality and false confidence;
- security/observability requirements;
- contract compatibility.

Review agents report findings and should be read-only by default. They do not silently repair the same code they are approving.

Independent review is a repository role. Use a fresh repository-local
read-only reviewer by default; global or custom review skills are optional and
require repository authorization. If such an optional skill requires
unrelated, unavailable, or repository-prohibited tooling, reject it and use
the repository-local reviewer instead. Only the absence of reviewer-subagent
capability, or a higher-priority platform instruction that prevents
repository-local review, blocks this gate.

Required review cycle:

```text
Implement → Integrate → Independent review → Remediate → Re-review → Gate passes
```

Findings should be reported with severity (`blocker`, `high`, `medium`, `low`), evidence, affected requirement/contract and recommended remediation. `blocker` and `high` findings prevent phase/slice completion.

Use one independent reviewer by default. Add a second independent perspective only when the changed scope materially includes security/secrets, cloud/infrastructure, persistence/migrations, substantial concurrency or another concrete high-risk surface:

1. spec/architecture/test reviewer;
2. security/operations reviewer when justified by that risk.

Do not create redundant reviewers solely to increase agent count.

A reviewer may later be assigned a separate remediation task, but once it authors that remediation it cannot be the sole reviewer approving that remediation.

If independent reviewer capability is unavailable for an execution whose kickoff requires it, the primary agent must report the limitation and stop before completion rather than self-certify.

## 12. Phase 0 guidance

Phase 0 begins sequentially only long enough for the primary agent to freeze repository layout, ownership boundaries and shared root conventions. After that point, multi-agent execution is mandatory when supported.

Phase 0 must use separate .NET and Python implementation agents/worktrees after the base layout is frozen, when the environment supports them. After integration, at least one independent reviewer must inspect the integrated bootstrap before Phase 0 can pass.

Additional parallel/read-only Phase 0 candidates include:

- read-only validation of .NET project layout;
- read-only validation of Python tooling;
- CI review;
- local infrastructure validation;
- documentation review.

Avoid concurrently editing the same solution/project files, compose files, root configuration or CI workflow from multiple worktrees unless ownership is clearly separable. Shared/root integration remains with the primary agent.

If subagent/worktree tooling is unavailable during the Phase 0 kickoff, the primary agent must report that fact and stop before code changes because the kickoff explicitly tests this operating model.

## 13. Stop conditions

The affected task must stop when:

- authoritative specs contradict one another;
- required product behavior is genuinely unspecified;
- a shared contract requires an unapproved material change;
- a new infrastructure/product dependency is required outside approved decisions;
- task completion requires deferred scope;
- integration would overwrite unresolved work from another agent.

Ordinary implementation choices should not trigger unnecessary human escalation.

## 14. Definition of done for multi-agent work

Multi-agent execution is complete only when:

- implementation occurred on a dedicated integration branch rather than directly on `main`;
- base/integration/task branch and worktree evidence is reported;

- delegated work is integrated;
- no worktree contains the only copy of required changes;
- repository-wide relevant checks pass;
- shared contracts agree across components;
- acceptance evidence exists;
- temporary worktrees/branches have a known disposition;
- at least one required independent review gate has passed;
- blocker/high review findings have been remediated and re-reviewed;
- the primary agent provides one coherent final report including reviewer verdicts.
