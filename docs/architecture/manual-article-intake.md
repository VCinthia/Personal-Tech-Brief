# Slice 8 — Manual article URL intake

This document freezes the implementation-level boundaries for Slice 8. It
implements **FR-003**, **UC-004** and **AC-011**: a user submits a single
article URL as a one-off input that enters the normal processing flow without
its host becoming a permanent subscribed source. It does not implement any
recurring arbitrary scraping, browser automation, or a new content feed.

## Scope

Deliver, on the .NET side only (Python content-intelligence is untouched):

- a public REST endpoint that accepts one article URL;
- an SSRF-safe fetch of that URL with bounded redirects, size and time;
- parse-only HTML extraction of a title plus readable main text;
- a source-less `SourceItem` (`OriginType = ManualUrl`, `SourceId = null`) plus
  its outbox message, committed in one transaction, reusing the existing durable
  `content-processing` handoff;
- deterministic duplicate detection so a URL already ingested (by feed or a
  prior manual submission) does not create a second item;
- a small Blazor page to submit a URL and see the outcome;
- unit + SQL integration tests, and a refreshed public OpenAPI baseline.

No permanent `Source` row is ever created or modified by this path (AC-011).

## REST contract (frozen)

`POST /api/v1/articles`

Request body:

```json
{ "url": "https://example.com/article" }
```

Responses:

- `202 Accepted` with body `{ "articleId": "<guid>", "status": "queued" }` when a
  new manual item is persisted and enqueued. `articleId` is the new
  `SourceItem.Id`.
- `202 Accepted` with body `{ "articleId": "<guid>", "status": "duplicate" }` when
  the URL is already known; `articleId` is the existing item's id when it can be
  resolved, otherwise omitted. No new item or outbox row is created.
- `400 Bad Request` (`ValidationProblem`, field `url`) when the URL is missing,
  malformed, not absolute `http`/`https`, or its host is reserved/localhost.
- `422 Unprocessable Entity` (`ProblemDetails`) when the URL is well-formed but
  cannot be turned into an ingestible item: host resolves only to
  non-public/blocked addresses, the fetch fails/times out/exceeds the size cap,
  a redirect leaves the safe set, the response is not `text/html`, or no usable
  title/text can be extracted. The detail never echoes fetched content.

The endpoint returns promptly; semantic analysis happens asynchronously through
the existing pipeline, so `202` (not `200`) is the success shape per
`12-api-contracts.md`.

## SSRF-safe fetch boundary

Reuse the Slice 3 outbound protections rather than re-inventing them:

- reject `localhost`/`*.localhost` names up front (`FeedHostAddressPolicy.IsLocalhostName`);
- resolve the host and require **every** resolved address to be a public,
  globally-routable unicast address (`FeedHostAddressPolicy.IsPublicInternetAddress`);
  a single unsafe answer rejects the host (no DNS-rebinding bypass);
- connect through a handler pinned to the pre-validated IP with
  `AllowAutoRedirect = false` and `UseProxy = false`
  (`PinnedAddressFeedValidationHttpClientFactory` pattern), preserving Host/SNI;
- follow redirects **manually**, bounded to `MaxRedirects`. Each hop's `Location`
  is parsed, constrained to absolute `http`/`https`, and its host is
  re-resolved and re-validated by the same policy before the next connection.
  A redirect to a non-public/blocked/localhost target aborts the fetch;
- enforce the request timeout and a maximum response byte cap
  (`BoundedReadStream`), and require a `text/html` (or `application/xhtml+xml`)
  content type;
- never log fetched article content, URLs' query strings, or bodies.

New bound options live in `ManualArticleFetchOptions` (section
`ManualArticleFetch`): `TimeoutSeconds` (default 10, 1–60),
`MaximumResponseBytes` (default 5 MB, ≤ 10 MB), `MaxRedirects` (default 5, 0–10),
`UserAgent`. These are configuration, never product behavior.

## Extraction boundary

Parse the fetched HTML with **AngleSharp** in parse-only mode (see D-024). No
script execution, no rendering. Extract:

- **title** — first non-empty of `<meta property="og:title">`, `<title>`, or the
  first heading; cleaned to `SourceItemText.CleanTitle` limits. If none exists,
  the submission is `422` (nothing ingestible).
- **main text** — a readability-lite selection: prefer `<article>`/`<main>`/the
  densest text container; drop `script`, `style`, `noscript`, `template`, `head`,
  `nav`, `header`, `footer`, `aside`; collapse whitespace; decode entities. The
  result is bounded to `SourceItemText.ExcerptMaxLength` (4000) and stored as the
  item's `Excerpt`. **Only extracted text is stored — never raw HTML.**
- **content hash** — SHA-256 over the normalized extracted text, stored as
  `ContentHash` for duplicate detection.

## Domain and persistence

- `SourceItem.CreateManual(originalUrl, title, excerpt, contentHash, retrievedAtUtc)`
  — a new factory that sets `SourceId = null`, `OriginType = ManualUrl`,
  `ExternalId = null`, and derives canonical/normalized URL exactly as the feed
  factory does. The existing private constructor's `sourceId == Guid.Empty`
  guard is relaxed so a null source is valid for the manual origin only.
- Persistence adds a manual path (`IManualArticleStore` / `SqlManualArticleStore`)
  that, in one transaction: runs the deterministic duplicate check (normalized
  URL → content hash → normalized-title window; the source+external-id rule
  cannot match a null source), and on miss adds the `SourceItem` + a pending
  `OutboxMessage` with the `SourceItemReady` envelope. It does **not** call
  `EnsureKnownSourceAndRun` and never touches `Sources`/`IngestionRuns`. See
  "Concurrency and duplicate integrity" for the isolation level and the unique
  index that make concurrent resubmissions safe.

## Durable messaging contract change (internal)

The version-one `content-processing` envelope and outbox are internal .NET
contracts changed atomically with producer and consumer (per
`ingestion-and-outbox.md`). To carry a source-less manual item:

- `OutboxMessage.SourceId` and `OutboxMessage.IngestionRunId` become `Guid?`
  (nullable); the non-empty guard applies only to `Id`, `SourceItemId`,
  `CorrelationId`. The feed path keeps passing non-null values unchanged.
- `SourceItemReadyEnvelope.SourceId` and `.IngestionRunId` become `Guid?`;
  `Create` still requires `MessageId`, `SourceItemId`, `CorrelationId` and a UTC
  timestamp, but permits null source/run. JSON serializes them as `null`.
- The inbox `AcceptAsync` reference checks already compare these fields for
  equality, which is null-safe: a manual item (`SourceItem.SourceId == null`)
  matches an envelope/outbox whose `SourceId == null`.
- EF: a migration alters `OutboxMessages.SourceId` and
  `OutboxMessages.IngestionRunId` to nullable (their FKs become optional).
  `SourceItems.SourceId` is already nullable. `has-pending-model-changes` must be
  clean after the migration.

The Processor consumes the manual item through the exact same handler/inbox path
as feed items; no new message type is introduced.

## Verification boundary

- Unit: URL validation; per-hop redirect re-validation and rejection; extraction
  (title/main-text selection, boilerplate stripping, bounds); envelope/outbox
  null-source acceptance; duplicate decisions. HTTP is exercised through
  controlled in-memory handlers — never a live network call.
- SQL integration (Testcontainers): a manual submission persists a `ManualUrl`
  item with null `SourceId`/run plus one outbox row atomically; a duplicate URL
  (including one already ingested via a feed) creates no second item; the
  Processor/inbox accepts the source-less envelope end to end; `202` responses
  carry the expected id/status.
- Contract: `docs/contracts/public-api.openapi.json` regenerated to add exactly
  `POST /api/v1/articles`, its request/response schemas, and the new status
  codes; the OpenAPI DeepEquals contract test stays green.

## Concurrency and duplicate integrity

A source-less manual item has no source/external-id unique key, so the normalized
URL is its authoritative duplicate key. A filtered unique index
`UX_SourceItems_ManualUrl_NormalizedUrlHash`
(`WHERE [OriginType] = 'ManualUrl' AND [NormalizedUrlHash] IS NOT NULL`, distinct
from and coexisting with the non-unique lookup index) enforces at most one manual
item per normalized URL. The manual store therefore runs at read-committed with a
best-effort pre-insert lookup, and a concurrent resubmission of the same URL loses
the insert on that index and is caught and returned as a `duplicate` — no
deadlock and no double insert.

## Out of scope (deferred)

Recurring scraping of the submitted host, JavaScript-rendered page execution,
authenticated fetches, non-HTML documents (PDF/feeds), and any change to
scheduled ingestion (Slice 9). Finishing this slice does not authorize starting
Slice 9.

## Independent review dispositions

Two independent read-only reviewers (spec/architecture/test; security/operations)
reviewed the integrated diff. No blocker/high findings. Dispositions:

- **Concurrent-duplicate correctness (medium) — fixed.** Added the filtered unique
  index above and switched the manual store to read-committed so the duplicate
  catch is live and deadlock-free; covered by a concurrent-submission SQL test.
- **Per-request `HttpClient`/handler leak (medium) — fixed.** The pinned requester
  now buffers a bounded body and disposes its client/handler on every path,
  returning a materialized response record.
- **Reserved IP literals returned 422 (low) — fixed.** A non-public IP-literal host
  is now rejected up front as `400` (matching the reserved-host contract), covered
  by unit tests.
- **Nullable source/run guard lacked a direct unit test (low) — fixed.** Added
  explicit envelope/outbox guard tests (null accepted; present-but-empty rejected).
- **Content-hash dedup is manual-vs-manual only (low) — accepted.** The guaranteed
  cross-origin dedup path is the normalized URL (tested feed→manual); content hash
  is a secondary best-effort key, matching the feed path.
- **Migration `Down` narrows nullable columns with a zero-guid default (low) —
  accepted.** Rollback-only and inherently lossy once source-less rows exist; the
  forward path is non-destructive.
- **No request rate limiting (low) — deferred to Slice 9 (operational hardening).**
  The address policy blocks all internal/metadata targets, so abuse is limited to
  fetching public hosts by the authenticated single user; throttling belongs with
  cloud operational hardening.
