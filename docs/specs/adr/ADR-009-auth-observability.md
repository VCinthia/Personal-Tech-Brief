# ADR-009 — Platform auth, managed identity and OpenTelemetry

**Status:** Accepted

## Context

The personal cloud deployment still needs protection and diagnostics, but building a custom identity system would add unrelated scope.

## Decision

- protect public cloud ingress using Azure Container Apps built-in authentication with Microsoft Entra ID;
- prefer managed identities/RBAC for Azure service access;
- use Key Vault only for remaining secrets;
- use OpenTelemetry in .NET and Python, exported to Azure Monitor/Application Insights in cloud.

## Consequences

- avoids custom password/account code;
- establishes cloud identity/security practice;
- enables cross-service correlation;
- local development requires a clearly isolated development auth path and local credentials/configuration.
