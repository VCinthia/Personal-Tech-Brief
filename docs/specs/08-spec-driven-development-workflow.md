# Personal Tech Brief — Spec-Driven Development Workflow

**Status:** Approved baseline

This workflow keeps implementation aligned with approved product intent while allowing controlled multi-agent execution.

## 1. Specification hierarchy

The hierarchy is:

```text
Product Definition
    ↓
Functional / Non-Functional Requirements
    ↓
Business Rules
    ↓
Use Cases
    ↓
Acceptance Criteria
    ↓
Architecture Decisions + Contracts
    ↓
Implementation Plan
    ↓
Tasks
    ↓
Code + Tests
```

Lower levels must not silently contradict higher levels.

## 2. Before implementation

Before a feature/slice is implemented, it should have:

- a clear requirement reference;
- relevant business rules;
- a use case or equivalent behavior description when needed;
- acceptance criteria;
- unresolved questions explicitly documented;
- architecture/technical decisions documented when the feature introduces a meaningful trade-off.

The primary agent must first identify the requested **phase/slice**, then map work to the relevant identifiers.

## 3. Requirement identifiers

Use stable identifiers:

- `FR-*` for functional requirements;
- `NFR-*` for non-functional requirements;
- `BR-*` for business rules;
- `UC-*` for use cases;
- `AC-*` for acceptance criteria;
- `D-*` for accepted decisions.

Implementation tasks may use local task identifiers, but those identifiers never replace requirement traceability.

## 4. Traceability

Implementation work must reference the requirements and acceptance criteria it satisfies.

Tests should make critical behavior traceable to product rules.

Example:

```text
Feature: finite brief generation
Implements: FR-011, BR-001, BR-002
Accepted by: AC-006, AC-007, AC-008
```

## 5. Slice planning

For each requested slice, the primary agent acts as technical integrator and should:

1. read the referenced specs/ADRs/contracts;
2. identify the relevant FR/NFR/BR/UC/AC identifiers;
3. decompose the slice into tasks;
4. build a dependency graph;
5. identify shared contracts that must be frozen first;
6. choose sequential or delegated execution deliberately;
7. define checks and handoff criteria for each task.

Do not create parallel work simply because multiple agents are available.

## 6. Multi-agent and worktree execution

The operational protocol is defined in `21-agent-orchestration-and-worktrees.md` and summarized by root `AGENTS.md`.

`main` is the stable promotion branch and is not a development workspace. Each non-trivial phase/slice uses a dedicated integration branch; concurrent implementation uses task branches/worktrees beneath that integration point. Review is performed against the integrated branch, and promotion to `main` is a separate explicit action after the quality gate.

To control token/context use, the primary agent owns broad context. Delegated agents receive root `AGENTS.md` plus only the specs/contracts relevant to their bounded task.

Core rule:

> **Parallelize implementation, not decisions.**

When concurrent agents write code:

- use separate worktrees/branches when supported;
- assign non-overlapping ownership;
- coordinate shared contracts before dependent work;
- require evidence-based handoffs;
- integrate and verify from the primary agent.

Independent passing worktrees do not establish slice completion until their combined result is integrated and verified.

## 7. Handling ambiguity

The primary agent should not invent product behavior when authoritative specs are genuinely ambiguous.

The sequence is:

1. identify the ambiguity and affected identifiers;
2. distinguish it from an ordinary implementation detail;
3. document the unresolved question/options;
4. update the specification/decision record when required;
5. implement only after intended behavior is explicit.

Do not ask questions already answered by the documents.

## 8. Handling discovered constraints

If implementation reveals a technical constraint that affects product behavior or a material shared contract:

1. do not silently work around it by changing behavior;
2. document the constraint;
3. record options/trade-offs;
4. update the relevant decision/specification when accepted;
5. then implement the chosen behavior.

Unrelated tasks may continue only if they do not prejudge the unresolved decision.

## 9. Scope control

A feature should not enter the MVP merely because:

- it is easy to add;
- a library already supports it;
- an agent has spare capacity;
- it demonstrates another technology;
- it appears in a tutorial;
- it might be useful someday.

It enters only if it supports an approved requirement or an explicitly accepted scope change.

No agent may continue automatically into a later slice.

## 10. Integration checkpoint

Before declaring a slice complete, the primary agent must:

1. integrate delegated changes in dependency order;
2. reconcile conflicts against the specs/contracts;
3. run formatting/lint/type checks;
4. build relevant components;
5. run unit/integration/contract/E2E checks required by the slice;
6. verify failure-path behavior;
7. verify observability/documentation requirements where applicable;
8. report evidence against acceptance criteria.

## 11. Definition of done for a slice

A slice is not complete only because code exists or individual agents report success.

Where applicable, completion includes:

- integrated implementation;
- automated tests;
- error/failure-path behavior;
- relevant observability;
- documentation/contract updates;
- acceptance criteria verified;
- no undocumented scope expansion;
- no required change stranded only in a worktree/branch.

## 12. Architecture baseline

The architecture baseline is approved and documented in:

- `10-system-design.md`;
- `11-data-model.md`;
- `12-api-contracts.md`;
- `13-processing-pipeline.md`;
- `14-testing-strategy.md`;
- `15-security-observability.md`;
- `16-local-cloud-environments.md`;
- accepted ADRs under `adr/`;
- `20-calibration-and-operational-defaults.md`;
- `21-agent-orchestration-and-worktrees.md` for execution isolation/orchestration.

The primary agent must implement `17-implementation-plan.md` one requested phase/slice at a time.


## Multi-agent and environment gate

Before implementation, the primary agent performs the environment/tool provenance preflight in `22-environment-isolation-and-tool-provenance.md`. Unrelated employer/project skills and tooling are prohibited.

When the requested phase/slice contains two or more genuinely independent implementation workstreams and collaboration tools are available, at least one implementation workstream must be delegated. Concurrent writers use isolated worktrees. Decisions/shared contracts are frozen by the primary agent before parallel implementation.


## Independent review stage

The implementation workflow for non-trivial code is:

```text
Spec → Decompose → Delegate → Implement → Integrate → Verify → Independent review → Remediate → Re-review → Complete
```

A slice is not complete solely because builds/tests pass or because the primary agent approves its own diff. At least one non-authoring reviewer must validate the integrated result when reviewer capability is available. Blocker/high findings are completion blockers.
