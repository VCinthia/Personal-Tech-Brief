# Source management boundaries

Slice 2 implements the configured RSS/Atom source resource only. It validates a
source endpoint before it can be stored or enabled; it does not retrieve items,
create ingestion runs, schedule polling, or enqueue work.

## Input and persistence rules

- A source name is trimmed and limited to 200 characters.
- A feed URL is trimmed, must be a well-formed absolute `http` or `https` URL,
  cannot contain credentials, and is limited to 850 characters after URI escaping
  and canonicalization. Canonicalization lowercases only scheme/host, removes a
  default port and fragments, and preserves path/query casing.
- Literal loopback, link-local, private, special-use, and `localhost` (including
  trailing-dot) targets are rejected before an outbound request.
- Active sources have a unique normalized feed URL. The SQL Server key uses a
  binary collation so path/query case remains distinct while canonical scheme/host
  values remain insensitive. Disabled sources remain in storage so future
  source-item/update/brief references remain traceable.

## Validation boundary

The validation boundary has a configurable 15-second default timeout, an explicit
product user agent, a 1 MiB default response limit, and cancellation propagation.
It resolves the host once for each validation, rejects the entire result when any
answer is non-public/special-use, and connects directly to a validated address.
The original host remains the HTTP Host header and HTTPS SNI value, while proxies
and redirects are disabled. XML parsing prohibits DTDs and external resolution.
A response is accepted only when it has a supported RSS structure (`rss` with a
channel) or Atom 1.0 structure (a feed with `id`, `title`, and `updated`).

Any syntactically safe URL that cannot be fetched or interpreted as one of those
feeds receives the public `422` ProblemDetails response and is not persisted as a
new source or applied as an update. The validator records only the host and a
failure category; it never logs the response body.

## Public behavior

The resource contract is generated at `/openapi/v1.json` and committed at
`docs/contracts/public-api.openapi.json`. `DELETE /api/v1/sources/{id}` is a
logical disable operation, consistent with the deletion/traceability policy; the
Sources UI leaves the row visible as inactive after that action.
