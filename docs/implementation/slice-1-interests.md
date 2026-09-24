# Slice 1 — Interests

## What was built

The first product slice implements an explicit, single-user interests list.
An interest has a specific technology-topic name, one of `High`, `Medium`, or
`Low` priority values, and an active state. The user can create, review, edit,
deactivate, and later reactivate an interest from both the REST API and the
small Blazor UI.

This delivers FR-001, BR-005, BR-006, UC-001, and AC-001. It intentionally
does not infer a user profile, ingest content, or display a content feed.

## Flow and ownership

```text
Blazor Interests page
        |  same-origin JSON /api/v1/interests
        v
ASP.NET Core endpoint
        v
Application service and repository port
        v
EF Core SQL Server repository + Interests table
```

The public endpoint validates request shape and returns standard
`application/problem+json` responses. The domain validates its own invariants
again, so callers other than HTTP cannot create an invalid interest. The
application service owns the use case and depends on a repository interface;
the infrastructure project owns the EF Core implementation. This keeps the
domain independent of SQL Server and makes the rules directly unit-testable.

## Important decisions and trade-offs

- `InterestName` trims the display name and stores a case-normalized form.
  Names are limited to 200 Unicode characters, which keeps the normalized SQL
  Server index within its key-size limit.
- A filtered, unique SQL Server index permits a name to be reused after an old
  interest is inactive while preventing two active preferences with the same
  normalized name. The application performs the same check to return a useful
  `409 Conflict`; the database remains the concurrency backstop.
- `DELETE` is a logical disable. Historical relevance work in later slices can
  therefore retain its association with a no-longer-active preference instead
  of losing traceability.
- The application does not apply EF migrations at startup. Production and
  local operators apply the committed migration explicitly, avoiding hidden
  database changes during an application restart.
- The Blazor page uses the first-class REST API rather than bypassing it with
  UI-only server calls. It stays deliberately small: list, form, status, and
  error states rather than a frontend framework or a design-system project.

## Testing and engineering notes

The slice demonstrates direct ASP.NET Core minimal routing, dependency
injection, cancellation-token propagation, ProblemDetails, EF Core mappings
and migrations, SQL Server filtered indexes, and layered application design.

Unit tests exercise value-object normalization, priority validation, timestamp
rules, and logical disablement. Fast API integration tests exercise the full
HTTP → application → relational persistence path with a disposable SQLite
database. A separate disposable SQL Server container applies the committed
migration with `MigrateAsync`, verifies readiness and the Interests API through
the production provider, and proves the filtered unique index translates a
racing duplicate write to the public `409 Conflict` contract. SQLite remains a
fast test tier only; the runtime provider and committed migration target SQL
Server/Azure SQL.

The generated public OpenAPI document is exposed at `/openapi/v1.json` and is
checked against the committed `docs/contracts/public-api.openapi.json` baseline.
The health probes deliberately split process liveness from SQL-backed readiness.

The UI demonstrates Blazor server interactivity, accessible labels and table
semantics, explicit loading/empty/saving/error states, and user-safe mapping
of ProblemDetails instead of exposing internals.
