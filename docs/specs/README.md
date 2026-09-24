# Personal Tech Brief — Specification Index

## Purpose

This directory is the canonical product, architecture and execution workspace for the **Personal Tech Brief** MVP.

The project follows **Spec-Driven Development**. Product intent is preserved first; implementation decisions are explicit; the primary agent works one approved slice at a time.

## Authority hierarchy

1. [`00-product-definition.md`](./00-product-definition.md) — canonical product intent.
2. Approved functional/non-functional requirements, business rules, use cases and acceptance criteria.
3. [`07-decision-log.md`](./07-decision-log.md) and accepted [`adr/`](./adr/) records.
4. Approved system design, contracts, pipeline, testing, security and environment documents.
5. [`17-implementation-plan.md`](./17-implementation-plan.md) — execution order.
6. Existing code.

No lower level may silently contradict a higher one.

## Documents

| File | Purpose | Status |
|---|---|---|
| `00-product-definition.md` | Canonical product definition and MVP intent | Baseline approved |
| `01-functional-requirements.md` | Functional requirements | Approved baseline |
| `02-non-functional-requirements.md` | Quality attributes and operational requirements | Approved baseline |
| `03-business-rules.md` | Product/domain rules | Approved baseline |
| `04-use-cases.md` | User/system use cases | Approved baseline |
| `05-acceptance-criteria.md` | MVP acceptance criteria | Approved baseline |
| `06-scope-and-roadmap.md` | MVP boundary and post-MVP hypotheses | Approved baseline |
| `07-decision-log.md` | Product and technical decision index | Active |
| `08-spec-driven-development-workflow.md` | Spec-to-code workflow | Approved baseline |
| `09-agent-working-agreement.md` | the primary agent guardrails | Approved operational baseline |
| `10-system-design.md` | Architecture and component ownership | Approved baseline |
| `11-data-model.md` | Persistence/domain data model | Approved baseline |
| `12-api-contracts.md` | Public and internal REST contracts | Approved baseline |
| `13-processing-pipeline.md` | Ingestion, async processing, grouping and selection | Approved baseline |
| `14-testing-strategy.md` | Unit/integration/contract/E2E approach | Approved baseline |
| `15-security-observability.md` | Security, identity and telemetry | Approved baseline |
| `16-local-cloud-environments.md` | Repo, local environment and Azure reference deployment | Approved baseline |
| `17-implementation-plan.md` | Phases and vertical slices | Approved execution plan |
| `19-agent-kickoff.md` | Exact first instruction for the primary agent | Ready |
| `20-calibration-and-operational-defaults.md` | Initial score/threshold/cadence/timeouts | Approved starting defaults |
| `21-agent-orchestration-and-worktrees.md` | Multi-agent delegation, worktree isolation and integration protocol | Approved operational baseline |
| repository root `AGENTS.md` | Repository-wide agent execution/orchestration rules | Ready |
| `adr/` | Architecture Decision Records | Accepted |

## Product invariant

The project must not optimize for feature count, source count, article count, engagement time, or notification frequency.

The product exists to **reduce information overload by exposing a small number of relevant, actionable technology updates from a larger set of processed information**.

A successful system may ingest a large amount of information while showing very little to the user.

## Change policy

1. Product-intent changes update `00-product-definition.md` first.
2. Affected requirements/rules/acceptance criteria are updated next.
3. Material technical decisions are captured in the decision log and ADRs.
4. Code follows the updated specification rather than silently creating new behavior.
5. the primary agent does not ask questions already answered by these documents.
6. Genuine blocking contradictions are surfaced before implementation changes product behavior.

## Start implementation

The documentation baseline is implementation-ready. Use [`19-agent-kickoff.md`](./19-agent-kickoff.md) with the primary agent. the primary agent begins with **Phase 0 only**, follows [`21-agent-orchestration-and-worktrees.md`](./21-agent-orchestration-and-worktrees.md), and must not proceed to Slice 1 until explicitly requested.

- `22-environment-isolation-and-tool-provenance.md` — mandatory greenfield isolation, external-skill quarantine and session provenance preflight.
