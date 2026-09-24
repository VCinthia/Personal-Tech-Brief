# Phase 0 Bootstrap Conventions

This document freezes the shared repository layout and ownership boundaries for
Phase 0. It records implementation conventions derived from the approved
specifications; it does not change product behavior or architecture decisions.

## Layout and ownership

- The primary integrator owns repository-root files, `compose.yaml`,
  `.github/workflows/`, `infra/`, `scripts/`, `docs/architecture/`, the root
  README, and final solution integration.
- The .NET bootstrap writer owns project and test contents under
  `src/dotnet/PersonalTechBrief.*/`, excluding the integration-owned
  `PersonalTechBrief.sln` file.
- The Python bootstrap writer owns
  `src/python/intelligence-service/`.
- The primary integrator creates the solution and adds the .NET writer's
  projects after handoff. Writers do not modify one another's paths or shared
  root configuration.

## Shared health contract

Both HTTP services expose these unauthenticated local/bootstrap probe routes:

- `GET /health/live` verifies that the process can serve requests without
  contacting external dependencies.
- `GET /health/ready` verifies bootstrap readiness. It must not call a live
  LLM or expose secrets, source content, or detailed configuration values.

The .NET web readiness policy may include its configured SQL Server dependency.
The Python readiness policy is configuration/initialization-only in Phase 0;
Python never accesses the product database.

## Scope boundary

Phase 0 creates only buildable service and infrastructure skeletons. It does
not add public product endpoints, persistence models or migrations, ingestion,
messages, intelligence behavior, user interface features, or any Slice 1
behavior.

## Container image updates

Phase 0 infrastructure and service Dockerfiles pin image digests so a future
pull cannot silently change the local environment. An image update is an
intentional maintenance change: update the image reference and digest together, record the
reason in the commit, rebuild both images, run `docker compose config`, start
the local infrastructure, and verify the Service Bus health endpoint before
accepting it.
