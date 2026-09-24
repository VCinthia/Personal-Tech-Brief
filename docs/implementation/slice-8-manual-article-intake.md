# Slice 8 — Manual article URL intake

## Delivered behavior

A user can submit a single public article URL for one-off analysis
(FR-003, UC-004, AC-011). The article is fetched safely, its title and main text
are extracted, and it enters the normal processing pipeline as a source-less
item — without the article's host ever becoming a permanent subscribed source.

## Flow and responsibility

1. `POST /api/v1/articles { "url": "..." }` (owned by .NET Web).
2. `ManualArticleSubmissionService` validates the URL (absolute http/https, not a
   reserved host or non-public IP literal → `400`), then fetches it.
3. `ArticleContentFetcher` performs an SSRF-safe fetch: resolve the host, require
   every address to be public, pin the connection to the validated IP, follow
   redirects manually with per-hop re-validation, and accept only a `text/html`
   response within the size/time caps. The transport (`PinnedArticleRequester`)
   returns a fully materialized, bounded response and disposes its client.
4. `ArticleTextExtractor` parses the HTML with AngleSharp (parse-only, no script
   execution) into a title + readable main text; only the extracted text is kept.
5. `SqlManualArticleStore` persists a source-less `ManualUrl` `SourceItem`
   (`SourceId = null`) plus its `content-processing` outbox message in one
   transaction, reusing the durable Slice 3 handoff. The Processor consumes it
   through the unchanged inbox path.
6. A small Blazor "Add article" page calls the endpoint and reports the outcome.

Responses: `202 { articleId, status: "queued" | "duplicate" }`; `400` for an
invalid/reserved URL; `422` when the URL cannot be fetched or no text extracted.

## Key decisions and trade-offs

- **Source-less item, not a synthetic source.** The data model made
  `SourceItem.SourceId` nullable and added `OriginType.ManualUrl` precisely so a
  manual article carries no owning source. The durable envelope/outbox were
  widened to nullable `SourceId`/`IngestionRunId` (feed path unchanged; a present
  identifier must still be non-empty), so the same queue/inbox handoff serves both
  origins with no new message type.
- **SSRF defense in depth reuses Slice 2/3 primitives.** The public-address policy
  and pinned-connection handler are shared with feed validation; the new part is
  bounded manual redirect following that re-resolves and re-validates every hop —
  closing the redirect and DNS-rebinding bypasses.
- **Parse-only extraction over a hand-rolled stripper.** A hardened HTML parser
  (AngleSharp, recorded as D-024) is both safer and more accurate than regex
  stripping of untrusted HTML; it never executes scripts or renders markup, and
  only bounded extracted text is stored.
- **URL is the manual duplicate key, enforced by a filtered unique index.** With
  no source/external-id key, `UX_SourceItems_ManualUrl_NormalizedUrlHash` makes
  concurrent resubmissions safe under read-committed: the loser fails the insert
  and is returned as a duplicate rather than deadlocking or double-inserting.

## Known limitations and accepted debt

- Content-hash dedup is effectively manual-vs-manual; the guaranteed cross-origin
  duplicate path (a URL already ingested by a feed) is the normalized URL.
- The nullable-columns migration `Down` is a lossy rollback-only path once
  source-less rows exist; the forward path is non-destructive.
- No per-request rate limiting yet: the address policy blocks all internal targets
  so abuse is limited to public-host fetches by the authenticated single user;
  throttling is deferred to Slice 9 operational hardening.
- JavaScript-rendered pages, authenticated fetches, and non-HTML documents are out
  of scope.

## Verification and independent review

Release build zero warnings/errors; 253 .NET unit tests (URL validation and
reserved-IP rejection, per-hop redirect re-validation, size/redirect caps,
extraction, orchestration, nullable-reference guards) and 74 integration tests on
disposable real SQL Server (source-less persistence + single outbox atomicity,
resubmit and feed→manual dedup, a concurrent-submission race, inbox acceptance of
the source-less envelope, and the documented `202/400/422` outcomes). `dotnet
format`, the EF pending-model check, the package vulnerability scan, and the
regenerated public OpenAPI contract test all pass; the Python service is
unchanged this slice.

Two independent read-only reviewers (spec/architecture/test; security/operations)
reviewed the integrated diff. No blocker/high findings. Two medium findings —
concurrent-duplicate correctness and a per-request `HttpClient` leak — were fixed
and re-reviewed to approval; the remaining low findings were fixed or given an
explicit disposition (see `docs/architecture/manual-article-intake.md`).

## Interview connections

SSRF and untrusted-content handling; idempotent, source-agnostic message
contracts; read-committed + a unique constraint as the correct idempotent-insert
pattern versus serializable range locks; and keeping a security-sensitive new
capability inside the existing architecture rather than adding a parallel path.
