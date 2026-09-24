# ADR-002 — .NET owns the product; Python owns stateless content intelligence

**Status:** Accepted

## Context

Both .NET and Python must have authentic responsibilities. Splitting arbitrary CRUD features by language would create distributed complexity without a meaningful boundary.

## Decision

.NET owns:

- domain/application behavior;
- SQL persistence;
- public REST API/UI;
- ingestion/orchestration;
- grouping/relevance/brief business decisions.

Python owns a stateless internal Intelligence API for:

- text processing/similarity;
- semantic analysis;
- structured GenAI generation.

Python does not connect to the product database.

## Consequences

- each language has a coherent reason to exist;
- domain ownership remains clear;
- Python can be tested/deployed independently;
- a network boundary and internal API must be maintained.

## Alternatives considered

- all-.NET: technically simpler but would not build the required Python software-development experience.
- Python directly sharing the database: fewer API calls but creates schema coupling and split domain ownership.
- arbitrary microservices per feature: rejected as unnecessary complexity.
