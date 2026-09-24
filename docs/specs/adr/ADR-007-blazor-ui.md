# ADR-007 — Keep the MVP UI in the .NET application using Blazor

**Status:** Accepted

## Context

The product needs four small UI surfaces, but frontend technology is not a target skill for this project.

## Decision

Use a small Blazor-based UI colocated with the ASP.NET Core web application.

The public REST API remains first-class and is not replaced by UI-only server calls as the only product contract.

## Consequences

- avoids an additional Node/SPA toolchain;
- keeps focus on .NET/Python/cloud/testing;
- UI remains intentionally modest rather than becoming a design-system project.

## Alternatives considered

- React/Vue/Angular: unnecessary scope for the learning objective.
- no UI: would make the personal product materially less usable and leave important product flows only in API tools.
