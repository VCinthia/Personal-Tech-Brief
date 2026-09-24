# Personal Tech Brief

Personal Tech Brief reduces technology-information overload by turning
many inputs into a very small, relevant brief. The delivered Interests and
Sources slices add explicit technology-topic preferences and validated RSS/Atom
source management. Slice 3 adds finite RSS ingestion and durable SQL/Service Bus
handoff while preserving the finite-brief product invariant.

## Everyday use (one double-click)

If you just want your brief, you do not need any commands:

1. **First time only:** copy `.env.example` to `.env`, set a `MSSQL_SA_PASSWORD`
   and `ACCEPT_EULA=Y` (see the Compose section below), and make sure Docker
   Desktop is installed.
2. **Every time:** double-click **`Brief.cmd`** in this folder. It starts the app,
   pulls fresh items from your sources, generates a brief, and opens it in your
   browser (`http://localhost:8080/brief`). The first run builds the images and
   takes a few minutes; later runs take under a minute.
3. **When you are done:** double-click **`Stop.cmd`** to shut it down (your
   interests, sources and briefs are kept). Or leave it running.

Tip: right-click `Brief.cmd` → *Send to → Desktop (create shortcut)* for a
one-click launcher on your desktop. Manage your topics under **Interests** and
your feeds under **Sources** in the app; each brief shows what is new since the
last one.

## Source of truth

- `AGENTS.md` — repository execution and isolation rules.
- `docs/specs/README.md` — specification authority hierarchy.
- `docs/specs/17-implementation-plan.md` — approved phase/slice order.
- `docs/specs/19-agent-kickoff.md` — Phase 0 orchestration instructions.
- `docs/specs/adr/` — accepted architecture decisions.

## Prerequisites

- .NET SDK `10.0.301` (enforced by `global.json`)
- Python `3.14`
- Docker Desktop with Linux containers enabled

## Run the whole app locally (Docker Compose)

For everyday local use, one command builds and runs the entire stack — SQL
Server, the Azure Service Bus emulator, the Python intelligence service (on its
deterministic fake provider, so no cloud and no cost), a one-shot database
migrator, the web app and the processor:

```powershell
Copy-Item .env.example .env
# Edit .env: set MSSQL_SA_PASSWORD and, after reviewing the licenses, ACCEPT_EULA=Y.
docker compose up -d --build
```

The `migrator` service applies all EF Core migrations once and exits; `web` and
`processor` then start. Open the UI at `http://localhost:8080` (change the host
port with `WEB_PORT` in `.env`) and use **Add article**, **Sources**,
**Interests**, **Brief** and **History**. A submitted article flows end to end —
`web → SQL outbox → Service Bus → processor inbox → grouping/relevance → a
`TechnologyUpdate`` — and `POST /api/v1/briefs` (the **Brief** page) then
composes a brief from the relevant updates.

Run one finite RSS ingestion pass on demand (the `tools` profile keeps it out of
the always-on services):

```powershell
docker compose run --rm ingestion
```

Stop the stack (keeping the SQL volume) or tear it down completely:

```powershell
docker compose stop
docker compose down            # remove containers, keep data
docker compose down -v         # also remove the SQL volume (fresh database)
```

Rebuild after code changes with `docker compose up -d --build`. If SQL logins
fail after changing `MSSQL_SA_PASSWORD`, remove the old volume with
`docker compose down -v` (SQL Server only sets the SA password on first
initialization of the data volume).

## Start local infrastructure only (host-based development)

For iterating on a single service with `dotnet run`/`uv run` on the host, start
just SQL Server and the Service Bus emulator and run the apps yourself. Copy the
checked-in example and choose a strong local-only password. Setting
`ACCEPT_EULA=Y` confirms that you have reviewed and accepted the SQL Server and
Azure Service Bus emulator license terms; `.env` is ignored by Git.

```powershell
Copy-Item .env.example .env
# Edit .env: set MSSQL_SA_PASSWORD and, after reviewing the licenses, ACCEPT_EULA=Y.
docker compose up -d sqlserver servicebus-emulator
$deadline = (Get-Date).AddMinutes(2)
do {
    try { $health = Invoke-WebRequest -UseBasicParsing http://localhost:5300/health } catch {}
    if ($health.StatusCode -eq 200) { break }
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $deadline)
if ($health.StatusCode -ne 200) { throw "Service Bus emulator did not become ready." }
```

The Compose stack starts SQL Server on port `1433` and the Azure Service Bus
emulator on AMQP port `5672` with its management/health endpoint on `5300`.

To stop the local containers while retaining the named SQL volume:

```powershell
docker compose stop
```

## .NET commands

```powershell
dotnet tool restore
dotnet restore src/dotnet/PersonalTechBrief.sln --locked-mode
dotnet build src/dotnet/PersonalTechBrief.sln --configuration Release --no-restore
dotnet test src/dotnet/PersonalTechBrief.sln --configuration Release --no-build
dotnet format src/dotnet/PersonalTechBrief.sln --verify-no-changes --no-restore
```

Before running the web application, store a local-only SQL Server connection
string in user secrets. Replace the placeholder password with the password you
put in `.env`; do not put it in an appsettings file or commit it.

```powershell
dotnet user-secrets set --project src/dotnet/PersonalTechBrief.Web "ConnectionStrings:PersonalTechBrief" "Server=localhost,1433;Database=PersonalTechBrief;User ID=sa;Password=<MSSQL_SA_PASSWORD>;Encrypt=False;TrustServerCertificate=True"
$env:TIH_MIGRATIONS_CONNECTION_STRING = "Server=localhost,1433;Database=PersonalTechBrief;User ID=sa;Password=<MSSQL_SA_PASSWORD>;Encrypt=False;TrustServerCertificate=True"
dotnet tool run dotnet-ef database update --project src/dotnet/PersonalTechBrief.Infrastructure --startup-project src/dotnet/PersonalTechBrief.Web
dotnet run --project src/dotnet/PersonalTechBrief.Web --launch-profile http
```

The web application listens on `http://localhost:5134`. It exposes
`GET /health/live`, `GET /health/ready`, its generated public contract at
`GET /openapi/v1.json`, the first-class Interests REST API at
`/api/v1/interests`, the Sources REST API at `/api/v1/sources`, the Briefs REST
API at `/api/v1/briefs`, the per-update interaction APIs under
`/api/v1/updates/{id}` (`PUT`/`DELETE .../feedback`, `PUT`/`DELETE .../saved`,
`POST .../sources/{sourceItemId}/open`), the read-only product-evaluation summary
at `/api/v1/evaluation/summary`, and the Blazor Interests UI at `/interests`,
Brief UI at `/brief` and finite Brief History at `/history`. The readiness probe
verifies the configured SQL persistence dependency, while liveness stays
process-only. Migrations are intentionally explicit; the application does not
change the database at startup.

The Blazor UI uses a self-hosted retro-editorial theme (`wwwroot/app.css`) with
its web fonts served from `wwwroot/fonts/` — there is no external font CDN, so
the UI has no third-party network dependency at load time. Font sources and
their SIL OFL 1.1 licensing are recorded in `wwwroot/fonts/NOTICE.md`.

`POST /api/v1/briefs` selects up to five relevant `TechnologyUpdate`s, returns
`202 Accepted` with the new brief's id, and generates the presentation text
asynchronously in a hosted background service that calls the internal Python
Intelligence API — so set `IntelligenceApi__BaseAddress` to the running Python
service for generation to complete. `GET /api/v1/briefs/current` returns the
latest completed brief (empty is valid); `GET /api/v1/briefs/{id}` and
`GET /api/v1/briefs?cursor=&limit=` read a specific brief and the history.

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5134/health/live
Invoke-WebRequest -UseBasicParsing http://localhost:5134/health/ready
```

### Ingestion and processing locally

Apply the migrations above before starting either process. Web user secrets do
not automatically configure the separate Ingestion or Processor executables.
In each terminal used for these processes, set the runtime SQL connection using
the same local connection string. If the migration variable was set in this
terminal, it can be reused directly:

```powershell
$env:ConnectionStrings__PersonalTechBrief = $env:TIH_MIGRATIONS_CONNECTION_STRING
# In a new terminal instead set it to the same local SQL connection string.
# Do not leave this variable empty or put a real password in repository files.
```

The ingestion executable performs one finite pass over enabled sources. It
records a run for each source, uses HTTP validators when available, and commits
each new normalized item with its outbox message in one SQL transaction.
To run it once, use:

```powershell
dotnet run --project src/dotnet/PersonalTechBrief.Ingestion --configuration Release --no-build
```

For local automatic ingestion, leave the repository launcher running. It invokes
that finite executable immediately and then every four hours. `Ctrl+C` stops
the launcher. No OS scheduled task is installed; Azure job scheduling is part
of the deployment slice.

```powershell
./scripts/run-local-ingestion.ps1
# Optional one-pass launcher check, using the same configured database:
./scripts/run-local-ingestion.ps1 -Once
```

Start the Processor in a second terminal with the runtime SQL variable set and
the local Service Bus emulator running. This connection string is the emulator's
development-only fixture value, not an Azure credential:

```powershell
$env:ServiceBus__ConnectionString = 'Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'
dotnet run --project src/dotnet/PersonalTechBrief.Processor --configuration Release --no-build
```

The Processor dispatches pending SQL outbox records and receives messages using
peek-lock settlement, durably retaining pending inbox work before acknowledging
normal delivery. It also drains the durable inbox through the grouping/relevance
pipeline: for each pending item it calls the internal Python Intelligence API to
analyze it, groups it with existing `TechnologyUpdate`s by deterministic
similarity, and stores a deterministic relevance score — so the Python service
must be running and reachable at `IntelligenceApi__BaseAddress` for grouping to
make progress. Run the Processor continuously during local use and stop it with
`Ctrl+C`. Only identifiers, bounded failure categories and operational counts
belong in normal logs. Inspect source status via `/sources` or
`GET /api/v1/sources`.

Configuration uses standard environment names (double underscores for nesting):
`FeedRetrieval__TimeoutSeconds` defaults to 15, `MaximumResponseBytes` to 1048576,
and `MaximumAttempts` to 3 under the same section. `OutboxDispatch__BatchSize`
defaults to 20 and `PollIntervalSeconds` to 5. The grouping consumer uses
`GroupingPipeline__BatchSize` (20), `PollIntervalSeconds` (5), `GroupingWindowHours`
(168), `MaxComparisons` (25), `MergeThreshold` (0.78) and `MaxProcessingAttempts`
(5); relevance weights/threshold live under `Relevance__Scoring__*`; and
`IntelligenceApi__BaseAddress` must point at the Python service. For Azure, the
Service Bus namespace/managed identity setup is documented in the later
deployment slice.

## Python commands

```powershell
py -3.14 -m pip install --user "uv==0.12.13"
Set-Location src/python/intelligence-service
py -3.14 -m uv sync --locked --all-groups
py -3.14 -m uv run ruff format --check .
py -3.14 -m uv run ruff check .
py -3.14 -m uv run mypy src tests
py -3.14 -m uv run pytest
py -3.14 -m uv run personal-tech-brief-api
```

The stateless internal Python service listens on `http://127.0.0.1:8001`. It
exposes `GET /health/live`, `GET /health/ready`, and the internal
`POST /internal/v1/items/analyze` and `POST /internal/v1/similarity` endpoints.
It has no database dependency. Semantic analysis runs through a deterministic
fake provider by default, so no live-model dependency exists; the Azure OpenAI
provider is used only when the `PERSONAL_TECH_BRIEF_AZURE_OPENAI_ENDPOINT`,
`_DEPLOYMENT`, and `_API_KEY` settings are all present (endpoint must be
`https://`). CI never contacts a live model. The typed .NET client for these
endpoints (`AddIntelligenceApiClient`) ships in Infrastructure but is not yet
wired into a request flow.

## Container checks

```powershell
docker build --file infra/containers/dotnet-web.Dockerfile --tag personal-tech-brief-web:local .
docker build --file infra/containers/python-intelligence.Dockerfile --tag personal-tech-brief-python:local .
```

GitHub Actions runs the .NET, Python, Compose-configuration, and container-build
checks in `.github/workflows/ci.yml`.

## Scope boundary

Phase 0 provides the solution/project layout, health probes, local
infrastructure, locked tooling, and CI skeleton. Slice 1 provides explicit
interests and Slice 2 provides source registration/validation. Slice 3 adds
RSS ingestion, deterministic deduplication, SQL outbox/inbox persistence, and
Service Bus handoff. Intelligence, briefs, feedback/history, manual URLs, and
deployment hardening remain in their separately tracked later MVP slices.

The [implementation guide](docs/implementation/README.md) explains
delivered flows, design decisions, test evidence and accepted checkpoints.
