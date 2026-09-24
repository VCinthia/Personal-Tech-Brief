# ADR-006 — Azure OpenAI / Foundry as reference GenAI provider behind an abstraction

**Status:** Accepted

## Context

The product benefits from semantic generation but must remain testable and must not make an LLM the owner of deterministic business rules.

## Decision

Use Azure OpenAI / a Microsoft Foundry-hosted OpenAI-compatible model as the reference cloud provider from the Python service.

The Python service defines a provider abstraction and validates structured model output through Pydantic/JSON Schema.

No live model calls are required by automated CI tests.

## Consequences

- strong Azure integration and structured outputs;
- provider/model can evolve without changing .NET domain behavior;
- prompt/schema/model versions need traceability;
- provider cost/failures must be handled explicitly.

## Alternatives considered

- direct provider calls from .NET domain/application code: rejected because it couples product logic to provider plumbing and weakens the Python boundary.
- local LLM as baseline: adds hardware/runtime variance without solving an MVP requirement.
