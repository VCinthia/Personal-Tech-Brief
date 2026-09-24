# Personal Tech Brief — Testing Strategy

**Status:** Approved implementation baseline

## Testing goals

Tests must protect product behavior and service boundaries, not merely implementation details.

The critical risks are:

- duplicates or retries producing extra visible updates;
- brief size/relevance rules being violated;
- broken source ingestion cascading across sources;
- .NET/Python contract drift;
- live GenAI dependencies making tests nondeterministic;
- historical brief content being mutated;
- error handling silently losing work.

## .NET tests

### Unit tests — xUnit

Prioritize:

- interest priority/domain validation;
- deterministic duplicate rules;
- processing state transitions;
- grouping threshold decision;
- relevance-score component calculation;
- brief selection max-five/no-fill/empty behavior;
- feedback/saved-state rules;
- outbox behavior at the application boundary.

Avoid mocking value objects/domain logic merely to achieve coverage.

### Integration tests

Use real infrastructure containers where practical:

- SQL Server;
- Azure Service Bus emulator for messaging integration tests;
- fake/stub HTTP server for feeds and Python service.

Exercise:

- EF Core mappings/migrations;
- API -> application -> SQL flow;
- ingestion idempotency;
- outbox publish flow;
- queue consumer idempotency;
- API ProblemDetails/error contracts.

### API contract tests

- verify public OpenAPI document is generated;
- verify important status codes and payloads;
- prevent accidental breaking contract changes where practical.

## Python tests — pytest

### Unit tests

Prioritize:

- text preprocessing;
- similarity calculations;
- threshold/boundary behavior;
- Pydantic validation;
- LLM response-schema parsing;
- prompt construction excluding unsupported/untrusted instructions from source text;
- deterministic fallback/error classification.

### Integration tests

- FastAPI endpoint tests through ASGI test client;
- mocked model-provider HTTP responses;
- timeouts/retries/error mapping;
- structured output validation failures.

No CI test suite should require paid/live LLM access.

## Cross-language contract tests

Maintain language-neutral OpenAPI/JSON schemas for internal endpoints.

CI should detect when:

- Python contract changes but .NET client expectations are stale;
- required fields diverge;
- enum values become incompatible.

## End-to-end smoke test

A deterministic fixture flow should prove:

1. configure interests;
2. add fixture RSS source;
3. run ingestion;
4. process queued items with fake intelligence provider;
5. group duplicate coverage;
6. generate a brief;
7. assert no more than five items and correct traceability;
8. submit feedback/save state;
9. retrieve history.

This test may run slower than unit tests but must remain fully local and deterministic.

## Test data

- synthetic/fixture feeds committed to the test project;
- avoid relying on live news sites in automated tests;
- include duplicate IDs, duplicate URLs, same-event/different-source samples, irrelevant content, malformed feed, timeout and partial failure cases.

## Quality gates

Before merge/accepted the primary agent increment:

- .NET build succeeds with warnings treated according to repository policy;
- .NET tests pass;
- Python formatting/lint/type checks pass;
- Python tests pass;
- architecture/contract docs updated when required;
- database migration generated/reviewed when model changes;
- no secret committed.

Coverage percentage is diagnostic, not the Definition of Done. Critical behavior coverage matters more than chasing a global number.
