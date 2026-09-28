# Personal Tech Brief

Turn many technology sources into one very small, relevant brief — so you stay
current without drowning in feeds.

<video src="https://github.com/VCinthia/Personal-Tech-Brief/raw/main/docs/media/personal-tech-brief-demo.mp4" controls width="720">
  Your browser can't play this embedded video.
  <a href="docs/media/personal-tech-brief-demo.mp4">View the demo</a>.
</video>

## What it is

Personal Tech Brief reduces technology-information overload. It may ingest a
lot, but it shows very little: a finite brief of **at most five** prioritized
updates, drawn from your explicit interests and trusted RSS/Atom sources. A
small — or even empty — brief is a success signal, not a failure.

- **Explicit interests**, each with a High / Medium / Low priority.
- **Trusted RSS/Atom sources**, validated when you add them.
- **Deduplication and grouping**, so the same event reported by many sources
  becomes a single update.
- **Deterministic relevance first**; expensive generative AI runs only on the
  few updates actually selected for the brief.
- Runs **fully local at no cost** on a deterministic fake AI provider — no
  cloud and no API keys required.

## Tech stack

**.NET 10** (ASP.NET Core, EF Core, Blazor, Generic Host workers) for the
domain, persistence, public REST API and orchestration · **Python 3.14**
(FastAPI) for the stateless content-intelligence service · **SQL Server** with a
transactional **outbox/inbox** · **Azure Service Bus** (emulator locally) for
durable handoff · **Docker Compose** for the full stack · **GitHub Actions** CI.

<details>
<summary><b>How it works (architecture)</b></summary>

<br>

The visible unit is a **relevant update**, not a raw article. Content flows
through one pipeline, and prioritization happens *before* any expensive
summarization:

```text
RSS/Atom sources
      ↓  finite ingestion (normalize + deduplicate)
   SQL outbox  ──►  Azure Service Bus  ──►  processor inbox (idempotent)
      ↓  analyze (Python) + group related items + deterministic relevance score
  TechnologyUpdates
      ↓  select top ≤ 5 relevant updates
   Brief  ──►  generate concise summaries (Python)  ──►  Blazor UI
```

Design choices worth noting:

- **Two runtimes, clear boundary.** .NET owns domain behavior, persistence, the
  public API and orchestration; Python is a stateless intelligence service with
  no database access.
- **Transactional outbox + durable inbox** give reliable, idempotent handoff
  between ingestion and processing (peek-lock settlement, retries, DLQ).
- **Deterministic rules before GenAI.** Enabled sources, date bounds, exact
  duplicates and the five-item limit are plain software; the model is used only
  where semantic reasoning adds value.
- **Finite brief invariant.** The product never becomes an infinite feed; zero
  relevant items is a valid outcome.

</details>

## Quick start (one double-click)

If you just want your brief, you do not need any commands:

1. **First time only:** copy `.env.example` to `.env`, set `ACCEPT_EULA=Y`, and
   set a `MSSQL_SA_PASSWORD` that meets SQL Server's password policy — at least
   8 characters with three of: uppercase, lowercase, digit, symbol (for example
   `LocalDev!Passw0rd`). Otherwise SQL Server exits on startup and the app never
   becomes ready. Make sure Docker Desktop is installed.
2. **Every time:** double-click **`Brief.cmd`**. It starts the app, pulls fresh
   items from your sources, generates a brief, and opens it in your browser
   (`http://localhost:8080/brief`). The first run builds the images and takes a
   few minutes; later runs take under a minute.
3. **When you are done:** double-click **`Stop.cmd`** to shut it down (your
   interests, sources and briefs are kept), or leave it running.

Tip: right-click `Brief.cmd` → *Send to → Desktop (create shortcut)* for a
one-click launcher. Manage your topics under **Interests** and your feeds under
**Sources**; each brief shows what is new since the last one.

## Prerequisites

- .NET SDK `10.0.301` (enforced by `global.json`)
- Python `3.14`
- Docker Desktop with Linux containers enabled

<details>
<summary><b>Run the whole app with Docker Compose</b></summary>

<br>

One command builds and runs the entire stack — SQL Server, the Azure Service Bus
emulator, the Python intelligence service (on its deterministic fake provider,
so no cloud and no cost), a one-shot database migrator, the web app and the
processor:

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
TechnologyUpdate` — and `POST /api/v1/briefs` (the **Brief** page) then composes
a brief from the relevant updates.

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

</details>

<details>
<summary><b>Host-based development (run services with dotnet / uv)</b></summary>

<br>

### Start local infrastructure only

For iterating on a single service with `dotnet run`/`uv run` on the host, start
just SQL Server and the Service Bus emulator and run the apps yourself. Setting
`ACCEPT_EULA=Y` confirms you have reviewed and accepted the SQL Server and Azure
Service Bus emulator license terms; `.env` is ignored by Git.

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
emulator on AMQP port `5672` with its management/health endpoint on `5300`. To
stop the local containers while retaining the named SQL volume: `docker compose stop`.

### .NET

```powershell
dotnet tool restore
dotnet restore src/dotnet/PersonalTechBrief.sln --locked-mode
dotnet build src/dotnet/PersonalTechBrief.sln --configuration Release --no-restore
dotnet test src/dotnet/PersonalTechBrief.sln --configuration Release --no-build
dotnet format src/dotnet/PersonalTechBrief.sln --verify-no-changes --no-restore
```

Before running the web application, store a local-only SQL Server connection
string in user secrets. Replace the placeholder password with the one you put in
`.env`; do not put it in an appsettings file or commit it.

```powershell
dotnet user-secrets set --project src/dotnet/PersonalTechBrief.Web "ConnectionStrings:PersonalTechBrief" "Server=localhost,1433;Database=PersonalTechBrief;User ID=sa;Password=<MSSQL_SA_PASSWORD>;Encrypt=False;TrustServerCertificate=True"
$env:TIH_MIGRATIONS_CONNECTION_STRING = "Server=localhost,1433;Database=PersonalTechBrief;User ID=sa;Password=<MSSQL_SA_PASSWORD>;Encrypt=False;TrustServerCertificate=True"
dotnet tool run dotnet-ef database update --project src/dotnet/PersonalTechBrief.Infrastructure --startup-project src/dotnet/PersonalTechBrief.Web
dotnet run --project src/dotnet/PersonalTechBrief.Web --launch-profile http
```

The web application listens on `http://localhost:5134`. It exposes
`GET /health/live`, `GET /health/ready`, its generated public contract at
`GET /openapi/v1.json`, the Interests REST API at `/api/v1/interests`, the
Sources REST API at `/api/v1/sources`, the Briefs REST API at `/api/v1/briefs`,
the per-update interaction APIs under `/api/v1/updates/{id}`
(`PUT`/`DELETE .../feedback`, `PUT`/`DELETE .../saved`,
`POST .../sources/{sourceItemId}/open`), the read-only product-evaluation summary
at `/api/v1/evaluation/summary`, and the Blazor UI at `/interests`, `/brief` and
`/history`. The readiness probe verifies the configured SQL persistence
dependency, while liveness stays process-only. Migrations are intentionally
explicit; the application does not change the database at startup.

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

### Ingestion and processing

Apply the migrations above before starting either process. Web user secrets do
not automatically configure the separate Ingestion or Processor executables. In
each terminal used for these processes, set the runtime SQL connection using the
same local connection string:

```powershell
$env:ConnectionStrings__PersonalTechBrief = $env:TIH_MIGRATIONS_CONNECTION_STRING
# In a new terminal instead set it to the same local SQL connection string.
# Do not leave this variable empty or put a real password in repository files.
```

The ingestion executable performs one finite pass over enabled sources,
recording a run per source, using HTTP validators when available, and committing
each new normalized item with its outbox message in one SQL transaction:

```powershell
dotnet run --project src/dotnet/PersonalTechBrief.Ingestion --configuration Release --no-build
```

For local automatic ingestion, leave the repository launcher running. It invokes
that finite executable immediately and then every four hours; `Ctrl+C` stops it.
No OS scheduled task is installed; Azure job scheduling is part of the deployment
slice.

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
delivery. It drains the durable inbox through the grouping/relevance pipeline:
for each pending item it calls the internal Python Intelligence API to analyze
it, groups it with existing `TechnologyUpdate`s by deterministic similarity, and
stores a deterministic relevance score — so the Python service must be running
and reachable at `IntelligenceApi__BaseAddress` for grouping to make progress.
Run it continuously during local use and stop it with `Ctrl+C`. Only
identifiers, bounded failure categories and operational counts belong in normal
logs. Inspect source status via `/sources` or `GET /api/v1/sources`.

### Python

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
`https://`). CI never contacts a live model.

</details>

<details>
<summary><b>Configuration reference</b></summary>

<br>

Configuration uses standard environment names (double underscores for nesting):
`FeedRetrieval__TimeoutSeconds` defaults to 15, `MaximumResponseBytes` to
1048576, and `MaximumAttempts` to 3 under the same section.
`OutboxDispatch__BatchSize` defaults to 20 and `PollIntervalSeconds` to 5. The
grouping consumer uses `GroupingPipeline__BatchSize` (20), `PollIntervalSeconds`
(5), `GroupingWindowHours` (168), `MaxComparisons` (25), `MergeThreshold` (0.78)
and `MaxProcessingAttempts` (5); relevance weights/threshold live under
`Relevance__Scoring__*`; and `IntelligenceApi__BaseAddress` must point at the
Python service. For Azure, the Service Bus namespace/managed identity setup is
documented in the later deployment slice.

</details>

<details>
<summary><b>Container checks & CI</b></summary>

<br>

```powershell
docker build --file infra/containers/dotnet-web.Dockerfile --tag personal-tech-brief-web:local .
docker build --file infra/containers/python-intelligence.Dockerfile --tag personal-tech-brief-python:local .
```

GitHub Actions runs the .NET, Python, Compose-configuration, and container-build
checks in `.github/workflows/ci.yml`.

</details>

<details>
<summary><b>Project documentation & scope</b></summary>

<br>

The project is built with spec-driven development; the authoritative documents
live under `docs/`:

- `AGENTS.md` — repository execution and isolation rules.
- `docs/specs/README.md` — specification authority hierarchy.
- `docs/specs/17-implementation-plan.md` — approved phase/slice order.
- `docs/specs/adr/` — accepted architecture decisions.
- [`docs/implementation/`](docs/implementation/README.md) — per-slice
  implementation notes: delivered flows, design decisions and test evidence.

**Scope.** The MVP delivers explicit interests, validated sources, finite RSS
ingestion with deterministic deduplication, SQL outbox/inbox persistence and
Service Bus handoff, the intelligence service, brief generation, feedback and
history. Deployment hardening and the Azure setup are tracked in later slices.

</details>

## License

© 2026 VCinthia. Licensed under the
[GNU Affero General Public License v3.0](LICENSE): you may use, modify and share
this project, but any modified version — including one run as a network service —
must remain open source under the same license and keep the original copyright
notices.
