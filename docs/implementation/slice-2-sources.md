# Slice 2 — Sources

## Delivered behavior

Sources adds the explicit RSS/Atom subscriptions used by later ingestion. The
Blazor `/sources` page and `/api/v1/sources` REST resource support creation,
editing, enablement and logical removal. An enabled source must first pass
feed validation. Disabling it retains its identity and history while excluding
it from subsequent ingestion. This implements FR-002, UC-002/003 and AC-002/003.

## Flow and responsibility

The endpoint checks request shape, the application service coordinates URL/name
validation and the external feed check, and the EF repository saves the Source.
The domain holds input invariants; Infrastructure owns DNS, HTTP, XML and SQL.
Python does not participate because feed validity is a deterministic question.

A failed feed check returns a useful ProblemDetails response without making the
source active. DELETE is a logical disable: the UI keeps the row visible as
inactive, making its meaning consistent across the immediate response and reload.

## Key decisions and trade-offs

- URL scheme, host and default ports are canonicalized; path and query case are
  preserved. `/Feeds.xml` and `/feeds.xml` can be different resources. SQL Server
  uses a binary collation on the normalized key so its default collation cannot
  accidentally merge those resources.
- The stored URL limit is checked after escaping/canonicalization and before
  network access. A short Unicode input can expand substantially when escaped.
- An application duplicate check provides a friendly conflict response, while
  a filtered unique SQL index protects against concurrent writes.
- Validating a hostname's text is insufficient for SSRF protection. The HTTP
  boundary resolves the host, rejects unsafe answers, and pins the connection
  to the approved IP. Host and TLS SNI remain the original hostname. Redirects
  and proxies are disabled; Azure's platform address `168.63.129.16` is denied.
- Feed reads have a configurable 15-second timeout and 1 MiB response bound.
  XML parsing prohibits DTDs and external entity resolution. Failure logs retain
  host/category information without copying the feed body.

These choices trade a little infrastructure code for an explicit, testable
outbound boundary. The implementation supports RSS and Atom only; it does not
register scraping adapters or additional provider types.

## Verification and independent review

The accepted Slice 2 tree passed 61 unit and 20 integration tests, with no skips,
including disposable SQL Server migration and case-sensitive uniqueness tests.
Locked restore, Release build, formatter, public OpenAPI comparison, pending EF
model check and the then-current package vulnerability scan passed.

Two independent reviewers initially found DNS-based SSRF, URL comparison/length,
logical-removal UI inconsistency and insufficient SQL migration coverage. Those
findings were fixed and re-reviewed. A subsequent Azure platform-IP finding was
also fixed and re-reviewed. Both final verdicts approved `a3205e6` with no
remaining findings. See [delivery checkpoints](./delivery-checkpoints.md).

## Interview connections

This slice demonstrates .NET dependency injection and cancellation, REST
resource/error semantics, application ports for external dependencies, SQL
collation and filtered indexes, and meaningful integration tests. It also
illustrates why mocked HTTP alone cannot prove DNS/connection safety, and why
SQLite tests cannot substitute for SQL Server migration evidence.
