# Personal Tech Brief — Intelligence Service

Stateless internal content-intelligence API. Alongside the health probes it exposes
the Slice 4 internal endpoints (FR-007/008/009):

- `POST /internal/v1/items/analyze` — deterministic preprocessing plus a replaceable
  semantic-analysis provider returning validated structured features.
- `POST /internal/v1/similarity` — deterministic batch similarity in `[0, 1]`
  (no model call).

Endpoints, schemas, and error mapping follow the frozen contract in
`docs/architecture/intelligence-api.md`. The service never connects to a database.

Semantic analysis runs behind a provider abstraction. Without Azure OpenAI settings
it uses the deterministic, offline `FakeSemanticAnalysisProvider`, so CI and local
development never require paid or live LLM access. The Azure OpenAI provider is
selected only when `PERSONAL_TECH_BRIEF_AZURE_OPENAI_ENDPOINT`,
`…_AZURE_OPENAI_DEPLOYMENT`, and `…_AZURE_OPENAI_API_KEY` are all set.

## Local checks

```powershell
py -3.14 -m uv sync --all-groups
py -3.14 -m uv run ruff format --check .
py -3.14 -m uv run ruff check .
py -3.14 -m uv run mypy src
py -3.14 -m uv run pytest
```

## Run locally

```powershell
py -3.14 -m uv run personal-tech-brief-api
```

The service listens on `127.0.0.1:8001` and exposes `GET /health/live` and
`GET /health/ready`.
