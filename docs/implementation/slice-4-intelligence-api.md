# Slice 4 — Intelligence API

## Delivered behavior

Slice 4 adds the internal, stateless Python Intelligence API and its typed .NET
client — the technical capabilities behind FR-007/008/009 (ADR-002, ADR-006),
without yet making any grouping, relevance, or brief-selection decision. Two
internal endpoints ship:

- `POST /internal/v1/items/analyze` — deterministic preprocessing plus a
  replaceable semantic-analysis provider that returns validated structured
  features (normalized keywords/event descriptors, topic list, per-interest
  match strengths, and an impact signal), each numeric field bounded to `[0,1]`.
- `POST /internal/v1/similarity` — deterministic batch similarity of one
  candidate against a bounded set of representatives, in `[0,1]` with an
  algorithm version, preserving request order, with no model call.

The expensive final-generation endpoint (`/internal/v1/updates/generate`) is
deliberately **not** built here; it belongs to Slice 6 and will reuse the
provider abstraction added now. The product invariant holds: this layer
produces signals for later reduction, never a feed.

## Flow and responsibility

- **Python owns stateless intelligence** (ADR-002): it never touches the product
  database and holds no persistence config. `.NET` owns the domain, the calls
  into this API, and every business decision built on the returned signals.
- **analyze**: bound content to a configured character budget → call the
  `SemanticAnalysisProvider` → validate structured output → assemble the
  response. The default provider is a deterministic fake (offline), so CI and
  local dev never need a live model. An Azure OpenAI provider implements the
  same protocol behind the abstraction and is selected only when its settings
  are present; its model output is validated with Pydantic before it leaves the
  service.
- **similarity**: cosine over normalized token/TF vectors — a pure function of
  the input.
- **.NET typed client** (`IIntelligenceApiClient` in Infrastructure) wraps both
  endpoints over `IHttpClientFactory`, mirrors the exact wire DTOs, propagates
  `CancellationToken`, enforces the per-endpoint timeout, and maps problem+json
  to a typed `IntelligenceApiException` that never surfaces provider/credential
  detail. It is registered via `AddIntelligenceApiClient` but not yet wired into
  Web/Processor request flows — later slices consume it.

## Key decisions and trade-offs

- **Provider abstraction with a deterministic fake as the CI default.** A
  protocol plus a fake keeps deterministic business rules independent of any LLM
  (ADR-006) and guarantees no paid/live model call in CI. The Azure provider is
  lazy-imported only when configured.
- **Structured model output is untrusted data.** The Azure provider validates
  the model's JSON through Pydantic before use, filters interest matches to the
  supplied ids, and the prompt explicitly frames content as data, not
  instructions. Invalid output maps to `502`, never a crash.
- **Timeout placement matters.** The 30s processor→Python request budget
  (spec §20) is enforced by the *calling* .NET client, not by a server-side
  umbrella. An initial design wrapped the whole provider call (including the 45s
  per-attempt LLM timeout and retries) in a 30s server ceiling, which made the
  per-attempt timeout and the retry policy unreachable; independent review
  caught it. The service now bounds each attempt (45s) and the attempt count
  (up to 3 attempts) instead, and total server work stays finite.
- **One shared contract artifact, validated from both sides.** The canonical
  request/response JSON lives in `docs/contracts/intelligence/`; the .NET
  contract tests and the Python `test_contract_fixtures` both round-trip the
  same files, so a field/casing/enum/order drift on either side fails that
  side's test.
- **Secrets never leave the process.** The Azure key is a `SecretStr`, read only
  at the header-build site, never logged, never placed in errors or telemetry;
  telemetry records only correlation id, endpoint, provider type, outcome and
  latency.

## Verification and independent review

The integrated candidate passed: Python `ruff format --check`, `ruff check`,
`mypy` (src and tests, strict), and 52 pytest with no live LLM; .NET Release
build with zero warnings/errors, 103 unit tests, 41 integration tests (including
11 cross-language contract tests that need no Docker), `dotnet format`
verification, the EF pending-model check, and the package vulnerability scan.

Two independent reviewers examined the integrated diff — a spec/architecture/
contract/test reviewer and a security/operations reviewer (justified by the
cross-service contract and the GenAI-provider/secrets surface). Security/ops
approved outright (secrets wrapped and never logged, model output validated,
provider failures bounded and mapped without leakage, no SSRF surface since the
endpoint is config-only), raising only low/informational items. The spec/contract
reviewer initially requested changes on two mediums — a server-side analyze
budget that made the per-attempt timeout/retry unreachable, and a one-sided
"cross-language" contract test — both fixed and re-reviewed to approval. No
blocker or high findings.

## Known limitations and accepted debt

- No live Azure OpenAI integration test (by design — CI must not use paid/live
  LLM); the Azure path is validated structurally via a mocked HTTP transport.
- The .NET client trusts the service's `[0,1]` ranges on deserialize (no
  defensive clamp); the Python service enforces them on produce.
- A generated OpenAPI/JSON-schema export as an additional CI drift gate (spec
  §14) is a later cross-language-tooling follow-up; today both sides pin to the
  shared JSON fixtures.
- The .NET client has no overall wall-clock budget across retries; it is bounded
  by attempts × per-attempt timeout plus the caller's `CancellationToken`.

## Interview connections

This slice demonstrates Python packaging and strict typing (Pydantic v2, mypy
strict, Ruff), FastAPI routers with problem+json error mapping, a provider
abstraction with a deterministic test double, GenAI structured-output validation,
async I/O with per-attempt timeouts and bounded backoff+jitter retries, a typed
.NET `HttpClient` via `IHttpClientFactory` with options validation and typed
exceptions, and cross-language contract testing against a single shared artifact.

**Why a fake provider instead of mocking the model in each test?** A protocol
plus a deterministic fake keeps the service's own logic (bounding, assembly,
error mapping, interest filtering) testable without any network, and makes "no
paid/live LLM in CI" a structural guarantee rather than a per-test discipline.

**Why put the 30s budget on the client, not the server?** The 30s is the
processor→Python request SLA; enforcing it again on the server as an umbrella
over the 45s-per-attempt LLM call would make the per-attempt timeout and retries
dead code. The caller owns its request deadline; the service owns its per-attempt
and attempt-count bounds.
