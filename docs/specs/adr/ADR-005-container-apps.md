# ADR-005 — Azure Container Apps as reference compute platform

**Status:** Accepted

## Context

The system has an HTTP web app, internal HTTP Python service, a long-running queue consumer and a scheduled finite ingestion job, all containerizable and low-volume.

## Decision

Use Azure Container Apps:

- public .NET web app;
- internal Python API;
- event-scaled .NET processor;
- scheduled Container Apps Job for ingestion.

## Consequences

- one managed container environment supports multiple workload shapes;
- KEDA-based scaling and scale-to-zero are available;
- avoids Kubernetes cluster administration;
- requires understanding Container Apps ingress, identity and scale configuration.

## Alternatives considered

- AKS: operationally excessive for MVP.
- VMs: unnecessary server lifecycle management.
- App Service: strong web option but less cohesive for the queue worker + scheduled jobs in one platform.
- Azure Functions: viable for event/timer functions, but the chosen containerized worker/services provide a more consistent multi-language development/deployment model.
