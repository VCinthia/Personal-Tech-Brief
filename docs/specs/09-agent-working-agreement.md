# Personal Tech Brief — Agent Working Agreement

**Status:** Approved operational baseline

This document defines repository-wide implementation guardrails for the primary agent. Root `AGENTS.md` is the executable summary; `21-agent-orchestration-and-worktrees.md` is the detailed multi-agent protocol.

## Authority order

1. `00-product-definition.md` — product intent.
2. Approved FR/NFR/BR/UC/AC documents.
3. Accepted decision log and ADRs.
4. System/data/API/pipeline/testing/security/environment designs and contracts.
5. The requested implementation phase/slice.
6. Existing code.

Lower levels do not silently override higher ones.

## Product invariants

The primary agent must preserve:

- information reduction rather than content maximization;
- at most five primary brief updates;
- valid empty briefs;
- no artificial filling;
- no infinite ingestion feed exposed as the main product;
- source traceability;
- GitHub and other deferred providers remain post-MVP unless specs change.

## Architecture invariants

- .NET owns product/domain state, public REST API, SQL persistence and orchestration.
- Python is a stateless internal content-intelligence API.
- Python must not directly access the product database.
- deterministic rules stay testable without live LLM calls.
- selected-only expensive generation is preferred.
- async consumers are idempotent.
- historical BriefItem snapshots are immutable product history.
- external content is untrusted.

## Scope authority

The primary agent must not independently introduce:

- GitHub Releases ingestion;
- arbitrary recurring scraping;
- vector databases;
- semantic search/RAG/chat;
- notifications;
- multi-user/organization support;
- mobile app;
- social features;
- Kubernetes/Dapr/event sourcing;
- extra brokers/caches/services without an accepted decision.

Propose rather than silently implement an out-of-scope capability.

## Primary-agent responsibility

For each requested phase/slice, the primary agent is the technical integrator. It owns:

- requirement/acceptance mapping;
- dependency-aware task decomposition;
- shared-contract coordination;
- delegation/worktree decisions;
- integration;
- final verification and evidence.

Delegating implementation never delegates final accountability.

## Agent/delegation policy

Delegation is encouraged only for truly separable work.

Every delegated task must have bounded ownership, explicit identifiers, required checks and an out-of-scope boundary.

Delegated agents must not independently change product behavior, material architecture, shared contracts or later-slice scope.

**Parallelize implementation, not decisions.** Shared REST/message/data contracts must be coordinated before dependent implementations proceed concurrently.

Use separate Git worktrees/branches for concurrent writing agents when supported. Never assign two writers to the same working tree. Do not use destructive Git operations or overwrite another agent's work.

Detailed rules: `21-agent-orchestration-and-worktrees.md`.

## Implementation behavior

For each requested phase/slice:

1. read the referenced specs and ADRs;
2. map planned work to requirement/acceptance identifiers;
3. decompose into dependency-aware tasks;
4. freeze shared contracts needed by multiple tasks;
5. decide sequential vs delegated/worktree execution;
6. implement only that phase/slice;
7. include error/failure-path behavior;
8. include appropriate tests and telemetry;
9. integrate delegated work;
10. run the checks introduced/relevant to the integrated change;
11. report evidence and unresolved technical risks;
12. do not proceed to the next slice unless explicitly instructed.

## Delegated handoff

A delegated task is not complete until it reports:

- changed files/components;
- FR/NFR/BR/UC/AC identifiers addressed;
- tests/checks and results;
- implementation assumptions/decisions;
- unresolved risks/blockers;
- branch/worktree/commit reference where applicable.

The primary agent must validate the integrated result; green checks in isolated worktrees are insufficient.

## Ambiguity protocol

Do not ask questions already answered by the documents.

When a genuine contradiction or missing behavior blocks implementation:

1. identify the conflicting identifiers/documents;
2. explain why this is not merely an implementation detail;
3. propose options and trade-offs;
4. pause only the affected work;
5. wait for or record the accepted spec/ADR decision before changing product behavior.

Non-blocking implementation details should be decided using existing architecture principles and documented when materially important.

## .NET conventions

- .NET 10 LTS;
- ASP.NET Core and EF Core directly;
- no MediatR initially;
- standard DI/configuration/logging;
- `CancellationToken` through async I/O where appropriate;
- domain independent of infrastructure;
- EF Core migrations committed;
- ProblemDetails for consistent API errors;
- xUnit for tests.

## Python conventions

- Python 3.14;
- FastAPI + Pydantic;
- explicit typing;
- idiomatic Python package layout;
- provider/model integrations behind testable interfaces/protocols;
- pytest;
- Ruff;
- repository-selected static type checker;
- no paid/live LLM requirement in CI.

## Database and messaging

- SQL Server/Azure SQL is the product system of record;
- schema changes require migrations;
- use transactional outbox for durable SQL-to-Service-Bus handoff;
- stable message IDs and idempotent consumers;
- DLQ/retry paths must be diagnosable.

## Secrets and security

- never commit secrets;
- cloud uses managed identity where possible;
- Key Vault for remaining secrets;
- manual URL fetching must be SSRF-aware;
- do not render or execute untrusted feed HTML;
- do not allow article content to become model instructions.

## Definition of Done

A slice is done only when applicable implementation, integrated tests, failure behavior, observability, documentation/contract changes and acceptance criteria are all addressed.

No required change may exist only in an unintegrated worktree.

Passing code without relevant behavioral evidence is not sufficient.


## External tooling isolation

This repository must remain independent from any unrelated project/employer tooling. Do not use external custom skills, MCP workflows, generators, scripts, templates or code from unrelated repositories unless this project's approved specs explicitly adopt them.

Repository-local instructions take precedence over unrelated user-level workflow conventions where the instruction hierarchy permits. If a higher-priority runtime/developer instruction conflicts and would force unrelated tooling, report the source and stop the affected path instead of silently using it.

At the start of implementation sessions, perform the provenance preflight defined in `22-environment-isolation-and-tool-provenance.md`.


## Independent review gate

For every non-trivial code phase/slice, the primary agent must obtain review from at least one agent that did not author the reviewed implementation when reviewer/subagent capability is available. The primary integrator cannot self-certify completion. Blocker/high findings must be remediated and re-reviewed. Cross-service/security/cloud/persistence/concurrency-heavy work should use two review perspectives when capacity exists. Review evidence is part of the Definition of Done.


## Protected main and context budget

- `main` is a stable promotion branch; the primary agent must not develop directly on it.
- Each non-trivial phase/slice uses a dedicated integration branch/worktree.
- Concurrent writing agents use dedicated task worktrees/branches.
- Independent review targets the integrated branch before promotion.
- Promotion to `main` is standing-authorized once the quality gate passes; a separate owner prompt is not required (see `AGENTS.md` "Phase closure and promotion protocol").
- The primary agent carries broad project context; delegated agents read only root `AGENTS.md` and the minimum specs/contracts named in their task packet.
- Do not multiply token use by requiring every agent to reread the complete documentation corpus.
