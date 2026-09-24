# Personal Tech Brief — Local Development and Cloud Deployment

**Status:** Approved implementation baseline

## Repository shape

```text
/
├── AGENTS.md
├── README.md
├── compose.yaml
├── .env.example
├── docs/
│   ├── specs/
    │   └── adr/
│   └── architecture/
├── src/
│   ├── dotnet/
│   │   ├── PersonalTechBrief.sln
│   │   └── ...projects
│   └── python/
│       └── intelligence-service/
├── infra/
│   └── bicep/
├── scripts/
└── .github/workflows/
```

The current numbered specification documents move under `docs/specs/` when the real repository is initialized. Their identifiers/content remain canonical.

## .NET project shape

Initial target:

```text
PersonalTechBrief.Domain
PersonalTechBrief.Application
PersonalTechBrief.Infrastructure
PersonalTechBrief.Web
PersonalTechBrief.Ingestion
PersonalTechBrief.Processor
PersonalTechBrief.UnitTests
PersonalTechBrief.IntegrationTests
```

Rules:

- Domain has no infrastructure dependency.
- Application depends on Domain and defines ports/interfaces where needed.
- Infrastructure implements persistence/messaging/external HTTP concerns.
- Web/Ingestion/Processor are composition roots.
- Do not create a separate project for every folder/concept.
- Do not introduce MediatR in the initial implementation.

## Python project shape

```text
intelligence-service/
├── pyproject.toml
├── uv.lock
├── src/personal_tech_brief/
│   ├── api/
│   ├── models/
│   ├── services/
│   ├── providers/
│   └── settings.py
└── tests/
```

Use:

- FastAPI;
- Pydantic/settings;
- pytest;
- Ruff;
- static typing checks;
- dependency lockfile.

Keep modules small and capability-oriented. Do not mimic the .NET layering mechanically where Python conventions suggest a simpler structure.

## Local infrastructure

Docker Compose provides infrastructure dependencies, not necessarily every source-code process during the inner loop.

Initial services:

- SQL Server;
- Azure Service Bus emulator and its required SQL dependency/configuration;
- optional local telemetry collector if useful later.

Developers may run .NET/Python services directly for debugging while infrastructure stays in containers.

## Configuration

Use environment-specific configuration with checked-in examples only.

- `.env.example` contains names/placeholders, never credentials;
- local secrets ignored;
- .NET uses standard configuration providers/user-secrets as appropriate;
- Python uses environment-backed settings;
- cloud uses managed identity + Key Vault references.

## Database migrations

EF Core migrations live in the .NET infrastructure/persistence area.

Rules:

- every schema change has a migration;
- migrations are committed;
- production/cloud migration is an explicit deployment step;
- never call `EnsureCreated` for the real persistent environment;
- integration tests may provision disposable databases through test infrastructure.

## Cloud environments

At minimum:

- local;
- Azure `dev`/personal environment.

A separate production environment is unnecessary for the MVP unless the project later becomes actively used enough to justify it.

## Azure resources

Provision through Bicep modules:

- resource group-scoped deployment entrypoint;
- Container Apps environment;
- Container Registry;
- web container app;
- processor container app;
- ingestion Container Apps Job;
- internal Python container app;
- Azure SQL logical server/database;
- Service Bus namespace/queue;
- Key Vault;
- Log Analytics / Application Insights / OpenTelemetry destination as selected;
- managed identities and RBAC assignments.

Avoid manual portal-only infrastructure configuration except exploratory learning; final reproducible state belongs in IaC.

## CI

GitHub Actions baseline:

1. .NET restore/build/test;
2. Python lock/install/lint/typecheck/test;
3. OpenAPI/contract consistency checks when introduced;
4. container build validation;
5. Bicep lint/build.

Deployment can initially be manual workflow dispatch after CI succeeds. Automatic deployment on every commit is not required.
