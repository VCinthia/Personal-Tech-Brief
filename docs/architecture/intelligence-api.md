# Slice 4 — Python Intelligence API (frozen contract)

This document freezes the internal Intelligence API contract for Slice 4 so the
Python service and the .NET typed client can be implemented in parallel. It
implements the technical capabilities required by FR-007/008/009 (ADR-002,
ADR-006). It does **not** perform grouping, relevance decisions, or final brief
selection/generation — those remain in .NET (Slice 5) and the expensive final
generation endpoint is deferred to Slice 6.

## Scope for this slice

Delivered internal endpoints (base path `/internal/v1`, internal-only in cloud):

- `POST /internal/v1/items/analyze` — deterministic preprocessing plus a
  replaceable semantic-analysis provider that returns validated structured
  features (topic/interest match strengths, impact signal, normalized
  keywords/event descriptors).
- `POST /internal/v1/similarity` — deterministic batch similarity of one
  candidate against a bounded set of representatives, in `[0,1]` with an
  algorithm version. No model call.
- Existing `GET /health/live` and `GET /health/ready` are unchanged.

Explicitly deferred:

- `POST /internal/v1/updates/generate` (concise title / summary / whyRelevant)
  is the expensive final-generation step tied to FR-011/012/014 and only runs
  for updates already selected by .NET. There is no selection in this slice, so
  it is built in Slice 6. The provider abstraction added here is what that slice
  will reuse.

## Conventions

- JSON bodies; UTF-8; UTC ISO-8601 timestamps.
- Errors use RFC 9457 problem+json (`application/problem+json`) with a stable
  `type`/`title`/`status`, mirroring the .NET ProblemDetails convention.
- Every request carries a `correlationId` (GUID string) echoed in the response
  and emitted on a W3C-compatible trace/log scope. No database credentials or
  persistence identifiers are sent; only correlation/business references
  (`sourceItemId`, `interestId`, `updateId`) needed for tracing.
- The service is stateless and never connects to the product database.

## `POST /internal/v1/items/analyze`

Request:

```json
{
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "item": {
    "sourceItemId": "…guid…",
    "title": "string (1..512)",
    "excerpt": "string|null (<=4000)",
    "content": "string|null (<=20000)",
    "language": "string|null (BCP-47 hint)"
  },
  "candidateInterests": [
    { "interestId": "…guid…", "name": "string (1..100)", "priority": "high|medium|low" }
  ]
}
```

- `candidateInterests` is bounded (0..100). Empty is valid (no interests to match).
- `content`/`excerpt` are bounded and truncated to the configured budget before
  any provider call (content-size limits, spec §20). Truncation is applied
  deterministically and does not error.

Response `200`:

```json
{
  "correlationId": "…guid…",
  "sourceItemId": "…guid…",
  "analyzerVersion": "analyze-1",
  "language": "en|null",
  "normalized": {
    "keywords": ["string"],
    "eventDescriptors": ["string"]
  },
  "topics": ["string"],
  "interestMatches": [
    { "interestId": "…guid…", "matchStrength": 0.0 }
  ],
  "impact": { "level": "high|medium|low|none", "confidence": 0.0 }
}
```

- `matchStrength`, `impact.confidence` are floats in `[0,1]`.
- `interestMatches` contains at most one entry per supplied `interestId`; ids not
  supplied are never invented.
- `analyzerVersion` identifies provider+prompt+post-processing version for
  traceability.

## `POST /internal/v1/similarity`

Request:

```json
{
  "correlationId": "…guid…",
  "candidate": { "text": "string (1..20000)" },
  "representatives": [
    { "updateId": "…guid…", "text": "string (1..20000)" }
  ]
}
```

- `representatives` is bounded (0..100). Empty yields an empty `results` list.

Response `200`:

```json
{
  "correlationId": "…guid…",
  "algorithmVersion": "similarity-1",
  "results": [
    { "updateId": "…guid…", "similarity": 0.0 }
  ]
}
```

- `similarity` is a deterministic float in `[0,1]` (e.g. cosine over normalized
  token/TF vectors). Same input ⇒ same output; no randomness, no model call.
- `results` preserves the request order of `representatives`, one entry each.
- Grouping/merge thresholds live in .NET (spec §20: merge `0.78`); the Python
  service only returns raw similarity and its `algorithmVersion`.

## `POST /internal/v1/updates/generate` (Slice 6)

The expensive final-generation step. Called by .NET only for a candidate already
selected for a brief, with the candidate's own source material. It produces a
concise title, a source-grounded summary, and a why-relevant explanation as
validated structured output. It must not invent facts beyond the supplied
sources.

Request:

```json
{
  "correlationId": "…guid…",
  "update": {
    "technologyUpdateId": "…guid…",
    "primaryTopic": "string (1..200)",
    "sources": [
      {
        "sourceItemId": "…guid…",
        "title": "string (1..512)",
        "excerpt": "string|null (<=4000)",
        "sourceUrl": "string|null",
        "publishedAtUtc": "2026-09-13T00:00:00Z|null"
      }
    ]
  },
  "matchedInterests": [
    { "interestId": "…guid…", "name": "string (1..100)", "priority": "high|medium|low" }
  ]
}
```

- `sources` is bounded (1..50); `matchedInterests` bounded (0..100). Source text
  is bounded/truncated to the configured budget before the provider call.

Response `200`:

```json
{
  "correlationId": "…guid…",
  "technologyUpdateId": "…guid…",
  "generationVersion": "generate-1",
  "title": "string (concise, <=200)",
  "summary": "string (source-grounded)",
  "whyRelevant": "string"
}
```

- `generationVersion` identifies provider+prompt+post-processing for traceability
  and is persisted on the `BriefItem`.
- The provider abstraction (ADR-006) is the same as analyze: a deterministic
  `FakeGenerationProvider` is the default so CI needs no live LLM; the Azure
  provider validates structured output via Pydantic (validation failure ⇒ 502).
- Timeout budget 60s per attempt (§20 final generation), up to 3 attempts with
  backoff+jitter on transient provider calls; configuration, traced.

## Error mapping

- `400` — malformed body / boundary validation (missing required field, wrong
  type, out-of-range length, bad GUID).
- `422` — syntactically valid but semantically unprocessable. For analyze,
  `candidateInterests` containing duplicate `interestId` values is treated as
  the representative 422 case.
- `502`/`503` — semantic-analysis provider failure/unavailable (analyze only),
  after the configured retries; problem+json with a non-sensitive title. Never
  leak provider/credential detail or raw prompt/content.
- `504` — provider timeout (analyze) after the configured per-attempt timeout.
- similarity has no external dependency and therefore no provider error paths.

## Provider abstraction (ADR-006)

- A `SemanticAnalysisProvider` protocol yields the structured analyze features.
- A deterministic `FakeSemanticAnalysisProvider` is the default in tests and
  local/dev without provider config; it produces stable outputs from the input
  so CI never needs paid/live LLM access.
- An `AzureOpenAiSemanticAnalysisProvider` implements the same protocol behind
  the abstraction. Its output is validated through Pydantic/JSON-schema before
  it leaves the service; validation failure maps to `502` (not a crash). It is
  selected only when provider settings are present; it is never exercised by CI.
- Timeouts/retries (spec §20) are configuration, not hardcoded, and are traced.
  The overall processor→Python request budget (30s for analysis) is enforced by
  the **calling .NET client**, not duplicated as a server-side umbrella: a
  server ceiling smaller than the per-attempt LLM timeout would make the
  per-attempt timeout and the retry policy unreachable. The Python service
  instead bounds each provider attempt (LLM per-attempt 45s) and the number of
  attempts (up to 3 attempts, i.e. 2 retries, with backoff+jitter on transient
  provider calls), so total server work is bounded without an inner/outer
  timeout contradiction.

## Telemetry / correlation

- Each request opens a logging/trace scope carrying `correlationId` and the
  endpoint; provider latency and outcome are recorded without logging raw
  content, prompts, or credentials.

## .NET typed client + contract tests

- A typed `IIntelligenceApiClient` in .NET Infrastructure wraps the two
  endpoints using `IHttpClientFactory`, with request/response DTOs mirroring the
  schemas above, `CancellationToken` propagation, the configured timeouts, and
  problem+json error mapping to a typed exception. Registered via DI; base
  address and timeouts are configuration.
- The language-neutral canonical fixtures live in `docs/contracts/intelligence/`
  and are the single shared source of truth. **Both** suites validate against
  them: the .NET `IntelligenceApiContractTests` deep-equals-round-trip the DTOs
  against these files (and assert casing/enum/required-field drift), and the
  Python `test_contract_fixtures` round-trips the same files through the Pydantic
  models. Because both implementations are pinned to the one artifact, drift in
  field names, casing, enum values, `[0,1]` ranges, or list order on either side
  fails that side's contract test — no live service needed. (A generated OpenAPI
  export as an additional CI drift gate remains a later cross-language-tooling
  follow-up per spec §14.)

## Verification boundary

- Python: pytest unit tests for preprocessing, deterministic similarity
  (including identical-text ⇒ 1.0, disjoint ⇒ 0.0, order preservation, empty
  representatives), analyze with the fake provider, structured-output validation
  failures, timeout/error mapping with a mocked provider HTTP layer. No live LLM.
- .NET: typed-client unit tests over a stubbed HTTP handler (success, problem+json
  error mapping, timeout/cancellation) plus cross-language contract tests.
- Ruff format/lint, mypy strict, and the .NET build/format/test gates apply.
