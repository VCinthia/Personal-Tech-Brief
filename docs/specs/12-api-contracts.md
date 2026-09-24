# Personal Tech Brief — API Contracts

**Status:** Approved contract baseline; exact OpenAPI schemas generated during implementation

## General REST conventions

- Base path: `/api/v1` for public/product API.
- JSON request/response bodies.
- UTC timestamps in ISO-8601.
- `ProblemDetails` (`application/problem+json`) for HTTP errors.
- Resource-oriented routes; internal commands do not leak into route names.
- Validate input at the boundary and domain invariants in the application/domain layer.
- Support `CancellationToken` across .NET async request paths.
- OpenAPI is generated and committed/exported in CI once stable.

## Public/product API

### Interests

- `GET /api/v1/interests`
- `POST /api/v1/interests`
- `GET /api/v1/interests/{id}`
- `PUT /api/v1/interests/{id}`
- `DELETE /api/v1/interests/{id}` — logical removal/disablement behavior documented by implementation

Create/update payload includes:

- `name`
- `priority` — `high|medium|low`
- `isActive` where relevant

### Sources

- `GET /api/v1/sources`
- `POST /api/v1/sources`
- `GET /api/v1/sources/{id}`
- `PUT /api/v1/sources/{id}`
- `DELETE /api/v1/sources/{id}`
- `POST /api/v1/sources/{id}/enable`
- `POST /api/v1/sources/{id}/disable`

Source responses expose latest ingestion status but not raw secrets or internal retry details.

### Manual article submission

- `POST /api/v1/articles`

Request:

```json
{
  "url": "https://example.com/article"
}
```

Semantics:

- accepts one-off input;
- does not subscribe to the host;
- returns `202 Accepted` if downstream processing is asynchronous;
- response includes a resource/processing identifier.

Fetching a user-submitted public article for one-off extraction is allowed. This does not create a recurring arbitrary-scraping provider.

### Current brief

- `GET /api/v1/briefs/current`
- `POST /api/v1/briefs` — request brief generation when the current candidate set permits it

Generation may return `202 Accepted` with status URI when work is asynchronous.

### Brief history

- `GET /api/v1/briefs?cursor=...&limit=...`
- `GET /api/v1/briefs/{id}`

History paginates briefs, not the complete ingestion corpus.

### Feedback and saved state

- `PUT /api/v1/updates/{id}/feedback`
- `DELETE /api/v1/updates/{id}/feedback`
- `PUT /api/v1/updates/{id}/saved`
- `DELETE /api/v1/updates/{id}/saved`
- `POST /api/v1/updates/{updateId}/sources/{sourceItemId}/open`

Feedback request:

```json
{
  "value": "relevant"
}
```

or

```json
{
  "value": "notRelevant"
}
```

## Common status-code guidance

- `200 OK` — successful read/update with body;
- `201 Created` — synchronously created resource;
- `202 Accepted` — accepted async processing;
- `204 No Content` — successful state removal/action without body;
- `400 Bad Request` — malformed/boundary validation failure;
- `401 Unauthorized` — cloud request lacks authentication;
- `403 Forbidden` — authenticated identity not allowed when applicable;
- `404 Not Found` — resource absent;
- `409 Conflict` — unique/domain state conflict, such as duplicate active interest/source;
- `422 Unprocessable Content` — may be used for syntactically valid requests that cannot satisfy semantic validation, provided usage is consistent.

## Python Intelligence API

The Python API is internal-only in cloud deployment.

Base path: `/internal/v1`.

### Analyze item

`POST /internal/v1/items/analyze`

Purpose:

- preprocess title/excerpt/content;
- identify topic/interest matches from supplied candidate interests;
- estimate impact/magnitude signal;
- produce normalized semantic features used by the .NET domain/application layer.

The request contains content plus only the interest data needed for analysis. It does not contain database credentials or persistence identifiers beyond correlation/business references needed for tracing.

### Similarity batch

`POST /internal/v1/similarity`

Purpose:

- compare one candidate text representation with a bounded set of potential update representatives;
- return deterministic similarity values and algorithm version.

No service-side product persistence.

### Generate selected update

`POST /internal/v1/updates/generate`

Purpose:

- generate a concise title;
- generate factual summary;
- generate human-readable `whyRelevant` explanation;
- return validated structured output.

Only source material for a candidate already selected by the .NET application is sent for this expensive generation step.

### Health

- `GET /health/live`
- `GET /health/ready`

## Internal API compatibility

- .NET and Python contracts are represented with explicit schemas and contract tests.
- Backward-incompatible changes require coordinated version change or atomic deployment.
- Do not share generated language models as the only source of truth; retain language-neutral schema/OpenAPI artifacts.
