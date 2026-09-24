# Personal Tech Brief — Security and Observability

**Status:** Approved implementation baseline

## Security model

### Public surface

Only the .NET web application has public ingress in the reference cloud deployment.

The Python Intelligence API is internal-only.

### User authentication

The deployed single-user app is protected using Azure Container Apps built-in authentication with Microsoft Entra ID.

Authorization remains intentionally simple: allow only the configured account/identity set. The domain model does not add multi-user concepts for the MVP.

Local development may use a development-only authentication bypass that cannot be enabled accidentally in the production environment.

### Service-to-service access

- Python service uses internal Container Apps ingress.
- Prefer managed identity/RBAC for Azure resources.
- Do not put cloud credentials in repository configuration.
- Development connection strings/keys are loaded from ignored local environment files or user-secrets equivalents.

### Secrets

Use Azure Key Vault references for secrets that cannot be eliminated through managed identity.

Potential secrets include third-party/LLM credentials if managed identity is unavailable for the selected provider mode.

### External content is untrusted

RSS/article text is untrusted data.

- never execute embedded code;
- do not render unsanitized HTML;
- do not treat article text as system/developer instructions to the LLM;
- prompts must delimit source content as data;
- use outbound-request protections for one-off URLs to reduce SSRF risk: allow `http/https`, reject loopback/private/link-local targets, enforce redirects/size/time limits.

### Database

- EF Core parameterized queries by default;
- least-privilege cloud identity;
- migrations executed through controlled deployment/maintenance step rather than every replica racing on startup;
- no secrets or full raw provider payloads in normal logs.

## Observability goals

Every important processing path should be answerable with:

- what happened?
- which source/item/update/brief was involved?
- where did it fail?
- was it retried?
- which external dependency was called?
- which model/prompt/algorithm version participated?

## Telemetry standard

Use OpenTelemetry across .NET and Python.

Emit:

- structured logs;
- distributed traces;
- metrics.

Cloud destination: Azure Monitor / Application Insights.

## Correlation

Propagate W3C trace context over HTTP and include a correlation/business identifier in queue messages.

Log identifiers where relevant:

- `sourceId`;
- `ingestionRunId`;
- `sourceItemId`;
- `technologyUpdateId`;
- `briefId`;
- `messageId`;
- `promptVersion` / `analysisVersion`.

Do not log entire article content or model prompts by default.

## Important metrics

Operational:

- ingestion runs success/failure;
- new source items per run;
- processing queue depth;
- processing duration;
- retry/DLQ counts;
- Python API latency/error rate;
- LLM call latency/error rate;
- brief generation duration/failure;
- SQL dependency failures.

Product-evaluation:

- items ingested;
- updates formed;
- duplicates/groups consolidated;
- candidates above threshold;
- selected count per brief;
- relevant/not-relevant feedback;
- save/open events;
- source distribution in selected items.

## Health checks

### .NET web

- liveness: process is alive;
- readiness: critical runtime dependencies required to serve product API are reachable according to defined policy.

### Worker/job

- log/metric execution health;
- do not make liveness depend on transient third-party source availability.

### Python

- `/health/live` does not require live LLM access;
- `/health/ready` validates service initialization/configuration but should avoid expensive provider calls on every probe.

## Alerts for a personal MVP

Keep alerts minimal:

- repeated ingestion failures across runs;
- messages accumulating in DLQ;
- sustained processing failure;
- brief generation repeatedly failing;
- abnormal external-model error rate/cost signal.

The product must not generate user-notification pressure merely because operational monitoring exists.
