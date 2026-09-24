# ADR-001 — Use .NET 10 LTS and ASP.NET Core

**Status:** Accepted

## Context

The product needs a maintainable web/API backend, relational persistence, worker processes and strong automated testing. The candidate also has prior professional .NET experience that should be made explicit rather than hidden behind an internal framework.

## Decision

Use .NET 10 LTS with ASP.NET Core and EF Core of the same major version.

The initial implementation uses standard ASP.NET Core DI, routing/controllers, middleware, configuration and hosted-service patterns directly. MediatR is not introduced initially.

## Consequences

Positive:

- current LTS platform;
- direct practice of mandatory .NET skills;
- first-party web, worker and EF ecosystem;
- fewer framework layers to reason about.

Trade-off:

- some command/query orchestration is written explicitly rather than delegated to a mediator library.

## Alternatives considered

- .NET 8: still supported at design time but near end of support relative to the project horizon.
- .NET 11 preview/RC: not selected because the project should start on stable LTS rather than preview software.
