# Personal Tech Brief — Decision Log

This file records product and later technical decisions that should not be rediscovered or silently changed during implementation.

---

## D-001 — Product optimizes for information reduction

**Status:** Accepted

**Decision:** The product is designed to reduce information load rather than maximize content exposure or engagement.

**Consequence:** Feature proposals that primarily increase visible content require explicit justification.

---

## D-002 — Single-user MVP

**Status:** Accepted

**Decision:** The MVP targets one user.

**Consequence:** Multi-user, organization, and collaboration concerns are deferred.

---

## D-003 — Update/event is the visible unit

**Status:** Accepted

**Decision:** The user-visible unit is a meaningful technology update/signal, not necessarily a single source article.

**Consequence:** Deduplication and grouping are first-class product behaviors.

---

## D-004 — RSS/Atom is the first permanent provider

**Status:** Accepted

**Decision:** RSS/Atom is the MVP's initial permanent source mechanism.

**Consequence:** Arbitrary scraping and platform-specific integrations remain outside the MVP.

---

## D-005 — Brief is finite, maximum five items

**Status:** Accepted

**Decision:** A brief contains at most five primary updates and may contain fewer or zero.

**Consequence:** The product must not fill capacity with low-value content.

---

## D-006 — No infinite feed

**Status:** Accepted

**Decision:** The MVP does not provide an endless content browsing experience.

**Consequence:** History focuses on previous selected briefs, not all ingested items.

---

## D-007 — Explicit preferences before learned personalization

**Status:** Accepted

**Decision:** MVP personalization uses explicit interests and priorities.

**Consequence:** Learned profiles, embeddings of the user, and personalized ML models are deferred.

---

## D-008 — Prioritize before expensive summarization

**Status:** Accepted

**Decision:** Where feasible, filtering and prioritization should reduce the candidate set before expensive generative processing.

**Consequence:** Architecture should not assume every ingested item requires an LLM summary.

---

## D-009 — GenAI is a supporting capability

**Status:** Accepted

**Decision:** GenAI is used where semantic reasoning provides value but does not replace deterministic rules.

**Consequence:** Product logic must remain testable without live model calls where deterministic behavior is expected.

---

## D-010 — GitHub Releases is post-MVP and shares the same brief

**Status:** Accepted

**Decision:** GitHub Engineering Digest is documented as a future signal-provider extension, not a separate feed.

**Consequence:** GitHub-derived items must compete for the same limited brief capacity.

---

## D-011 — Technology follows product needs

**Status:** Accepted

**Decision:** .NET, Python, cloud services, queues, databases, and GenAI products will be selected based on requirements and trade-offs rather than inserted solely to showcase a technology.

**Consequence:** Architecture decisions require explicit rationale.

---

## D-012 — .NET 10 LTS is the primary application platform

**Status:** Accepted

**Decision:** Use .NET 10 LTS with ASP.NET Core and EF Core; expose core framework concepts directly rather than introducing MediatR initially.

**ADR:** `adr/ADR-001-dotnet-10-aspnet-core.md`

---

## D-013 — .NET/Python boundary follows capability ownership

**Status:** Accepted

**Decision:** .NET owns domain/application state and orchestration; Python exposes stateless text-intelligence capabilities and does not access product persistence directly.

**ADR:** `adr/ADR-002-dotnet-python-boundary.md`

---

## D-014 — SQL Server/Azure SQL is the system of record

**Status:** Accepted

**Decision:** Use SQL Server locally and Azure SQL Database in Azure through EF Core.

**ADR:** `adr/ADR-003-azure-sql.md`

---

## D-015 — Service Bus is the initial asynchronous boundary

**Status:** Accepted

**Decision:** Use one Azure Service Bus processing queue with application-level idempotency and DLQ diagnostics.

**ADR:** `adr/ADR-004-service-bus.md`

---

## D-016 — Azure Container Apps is the reference compute platform

**Status:** Accepted

**Decision:** Deploy web, internal Python API and processor as Container Apps; run ingestion as a scheduled Container Apps Job.

**ADR:** `adr/ADR-005-container-apps.md`

---

## D-017 — GenAI provider is abstracted behind Python

**Status:** Accepted

**Decision:** Use Azure OpenAI/Microsoft Foundry as reference provider through a Python provider boundary with structured outputs and deterministic test doubles.

**ADR:** `adr/ADR-006-genai-provider.md`

---

## D-018 — UI remains intentionally small and .NET-based

**Status:** Accepted

**Decision:** Use a small Blazor UI colocated with the ASP.NET Core application; do not add a separate SPA stack for MVP.

**ADR:** `adr/ADR-007-blazor-ui.md`

---

## D-019 — SQL-to-broker handoff uses transactional outbox

**Status:** Accepted

**Decision:** Persist new source items and outbox messages atomically before asynchronous publication to Service Bus.

**ADR:** `adr/ADR-008-transactional-outbox.md`

---

## D-020 — Use platform identity and OpenTelemetry

**Status:** Accepted

**Decision:** Use Container Apps built-in Entra authentication, managed identity/Key Vault, and OpenTelemetry/Azure Monitor for the cloud reference deployment.

**ADR:** `adr/ADR-009-auth-observability.md`

---

## D-021 — Multi-agent execution uses bounded delegation and isolated worktrees

**Status:** Accepted

**Decision:** The primary agent acts as technical integrator. Independent implementation may be delegated to bounded agents and isolated Git worktrees/branches when beneficial, but shared decisions/contracts are coordinated first and integrated verification remains centralized.

**Consequence:** Multi-agent execution cannot redefine product/architecture by consensus or accident. Worktree isolation is used to prevent concurrent writers from corrupting a shared working tree; it is not used to force unnecessary parallelism.

**Protocol:** `21-agent-orchestration-and-worktrees.md`


### D-022 — Mandatory delegation for independent implementation work

**Status:** Accepted

When the primary agent exposes subagent/worktree capabilities and a requested phase/slice contains at least two genuinely independent implementation workstreams, delegation is required. Phase 0 explicitly delegates .NET and Python bootstraps after shared repository layout is frozen. The primary agent remains integrator.

### D-023 — Greenfield environment/tool isolation

**Status:** Accepted

Personal Tech Brief must not use unrelated project/employer skills, scripts, MCP workflows, templates, code or scaffolding. Every implementation session performs a tool/instruction provenance preflight; conflicts with higher-priority external instructions are surfaced before invocation.

---

## D-024 — AngleSharp for server-side manual-article HTML extraction

**Status:** Accepted

**Decision:** Slice 8 (manual article URL, FR-003) parses fetched article HTML with the AngleSharp library to extract a title and readable main text. AngleSharp is a standards-compliant, actively maintained .NET HTML parser used in parse-only mode; scripts are never executed and HTML is never rendered.

**Rationale:** FR-003/UC-004 authorize one-off extraction of a user-submitted public article. Robust extraction of a title plus the main content (ignoring navigation/boilerplate) needs a real HTML DOM. A hand-rolled regex/tag stripper over untrusted HTML is both fragile and a security anti-pattern; a hardened, widely-used parser is the safer choice and satisfies the security requirement to never render unsanitized HTML.

**Scope/consequence:** AngleSharp is added only to the .NET Infrastructure project for manual-article extraction. It is not used for RSS/Atom feed validation (which stays on the existing bounded `XmlReader` path) and adds no scripting engine (no `AngleSharp.Js`). Extraction is bounded to the existing `SourceItem` shape — a title plus a main-text excerpt bounded to `SourceItemText.ExcerptMaxLength` — so no raw HTML is stored. This authorizes the dependency under AGENTS.md ("public dependencies explicitly allowed by the specs/ADRs").
