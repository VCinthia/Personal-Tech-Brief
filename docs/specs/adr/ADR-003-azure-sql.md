# ADR-003 — SQL Server locally and Azure SQL Database in cloud

**Status:** Accepted

## Context

The domain contains clear relational concepts, constraints and historical relationships: sources, items, grouped updates, interests, briefs and feedback.

## Decision

Use SQL Server for local development and Azure SQL Database for cloud deployment, accessed through EF Core from .NET.

## Consequences

Positive:

- relational integrity and transactions fit the model;
- managed Azure SQL reduces database operations burden;
- direct practice of SQL Server, a desirable vacancy skill;
- consistent SQL Server engine family between local/cloud.

Trade-offs:

- relational migrations/index design must be maintained;
- Azure SQL is more infrastructure than an embedded DB for local prototypes, but the project intentionally exercises real persistence behavior.

## Alternatives considered

- PostgreSQL: technically valid and familiar from prior experience, but offers no stronger fit for this domain and does not deepen the SQL Server gap.
- Cosmos DB: no current need for document-first/global-distribution patterns.
- SQL Server on VM: unnecessary DB administration burden.
