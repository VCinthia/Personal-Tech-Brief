# Personal Tech Brief — Implementation Plan

**Status:** Approved execution plan

The primary agent should implement the MVP as vertical, reviewable increments. It must not attempt the whole product in one pass.

## Phase 0 — Repository bootstrap

Goal: reproducible development skeleton with both language stacks and infrastructure foundations.

Deliver:

- repository structure from `16-local-cloud-environments.md`;
- move/copy approved specs into `docs/specs` without rewriting product intent;
- .NET 10 solution/projects;
- Python 3.14 FastAPI project;
- formatting/lint/type/test tooling;
- SQL Server local compose service;
- Service Bus emulator local compose service if stable in target environment;
- health endpoints;
- CI skeleton;
- root README with exact local commands;
- architecture decision records copied/created;
- no product feature beyond health/skeleton.

Exit criteria:

- .NET builds/tests;
- Python checks/tests;
- local infrastructure starts;
- both services can be launched;
- no secrets committed.

## Slice 1 — Interests

Implements: FR-001, BR-005, BR-006, AC-001.

Deliver end-to-end:

- EF model/migration;
- application behavior;
- REST endpoints;
- minimal Interests UI;
- validation/error contracts;
- unit/integration tests.

Learning focus: ASP.NET Core routing/controllers, DI, EF Core, SQL Server, async, validation, REST, tests.

## Slice 2 — Sources

Implements: FR-002, UC-002/003, AC-002/003.

Deliver:

- source persistence;
- feed validation;
- enable/disable;
- source status UI/API;
- controlled HTTP client/timeouts;
- tests with fixture HTTP responses.

Learning focus: `HttpClientFactory`, external API resiliency, REST resource semantics, integration testing.

## Slice 3 — RSS ingestion + durable async handoff

Implements: FR-004/005/006, UC-005, AC-004, NFR-004/005.

Deliver:

- ingestion job;
- feed parsing/normalization;
- ingestion runs;
- deterministic duplicate handling;
- transactional outbox;
- Service Bus publisher;
- processor consumer skeleton;
- retry/DLQ observability;
- local emulator integration tests where reliable.

Learning focus: worker services, idempotency, transaction boundaries, queues, eventual consistency, cloud messaging.

## Slice 4 — Python Intelligence API

Implements technical capabilities required by FR-007/008/009 without final product selection yet.

Deliver:

- typed FastAPI endpoints;
- text preprocessing;
- deterministic batch similarity implementation;
- semantic analysis provider interface;
- fake provider for tests;
- Azure OpenAI/Foundry provider behind abstraction;
- structured-output validation;
- .NET typed client + contract tests;
- telemetry/correlation.

Learning focus: Python packaging, typing, Pydantic, FastAPI, async I/O, pytest, dependency injection patterns, provider abstraction, GenAI structured outputs.

## Slice 5 — Grouping and relevance

Implements: FR-007/008/009/010/017, BR-004/009/010/011.

Deliver:

- TechnologyUpdate grouping;
- source associations;
- interest matches;
- deterministic relevance scoring;
- explainable score components for diagnostics;
- idempotent reprocessing;
- unit/integration tests including threshold boundaries.

Learning focus: application design, algorithms/trade-offs, cross-service integration, database indexing/querying.

## Slice 6 — Brief generation

Implements: FR-011/012/014/018, UC-007/008, AC-006/007/008/009/010/015/016.

Deliver:

- brief candidate selection;
- max-five/no-fill/empty rules;
- selected-only GenAI generation;
- immutable BriefItem snapshots;
- current brief REST/UI;
- failure/retry behavior;
- tests.

Learning focus: business-rule ownership, structured GenAI generation, cost control, consistency, REST async semantics.

## Slice 7 — Feedback, save, history, evaluation signals

Implements: FR-013/015/016, UC-009/010/011, AC-012/013/014.

Deliver:

- feedback and save state;
- source-open signal;
- brief history;
- basic product evaluation queries;
- finite-history UI without corpus feed.

Learning focus: API semantics, persistence modeling, product metrics vs engagement metrics.

## Slice 8 — Manual article URL

Implements: FR-003, UC-004, AC-011.

Deliver:

- one-off URL submission;
- SSRF-safe fetch/extraction boundaries;
- normal pipeline handoff;
- no permanent source creation;
- tests.

This comes later because safe arbitrary URL fetching adds security complexity and is not needed to prove RSS ingestion first.

## Slice 9 — Azure deployment and operational hardening

Implements cloud/reference architecture and NFRs.

Deliver:

- Bicep infrastructure;
- images in ACR;
- Container Apps deployment;
- scheduled ingestion job;
- Service Bus/KEDA processor scaling;
- Azure SQL;
- internal Python ingress;
- Entra/EasyAuth for web;
- managed identity/Key Vault;
- OpenTelemetry/Azure Monitor;
- smoke tests and deployment runbook.

Learning focus: cloud product selection, identity, networking, scaling, managed services, observability, deployment.

## Slice rule

For every slice the primary agent must:

1. cite requirement/acceptance IDs in its implementation plan;
2. identify affected ADR/contracts first;
3. implement the smallest coherent vertical path;
4. run relevant checks;
5. report design decisions and trade-offs;
6. update docs only when the implementation introduces an approved technical detail;
7. stop rather than silently expanding product scope.
