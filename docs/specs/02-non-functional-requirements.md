# Personal Tech Brief — Non-Functional Requirements

**Status:** Approved baseline  
**Depends on:** `00-product-definition.md`

These requirements define quality attributes without prematurely selecting architecture or cloud products.

## NFR-001 — Maintainability

The implementation should separate concerns clearly enough that ingestion, normalization, relevance evaluation, GenAI-assisted processing, persistence, and presentation can evolve independently where justified.

Business rules should not be unnecessarily coupled to provider-specific integrations.

---

## NFR-002 — Testability

Deterministic business rules must be testable without requiring live external feeds or live LLM calls.

External integrations should support substitution with controlled test doubles or equivalent fixtures for automated tests.

The test strategy should include appropriate unit and integration coverage for critical flows.

---

## NFR-003 — Observability

The system should make processing failures diagnosable.

At minimum, the implementation should support structured visibility into:

- ingestion attempts and failures;
- item processing state;
- brief generation failures;
- external dependency failures;
- GenAI invocation failures where applicable.

The reference logging/monitoring products are selected in `15-security-observability.md`; this requirement remains provider-agnostic at the product level.

---

## NFR-004 — Reliability

Failure to process one source item should not unnecessarily invalidate all unrelated source processing.

The system should preserve enough state to avoid repeatedly reprocessing the same item as new after recoverable failures.

---

## NFR-005 — Idempotency where appropriate

Operations that may be retried, especially ingestion and downstream processing, should be designed so that retries do not create unintended duplicate content or duplicate brief entries.

---

## NFR-006 — Performance

The user-facing brief and configuration views should respond within a reasonable interactive time for a personal-use MVP.

Long-running ingestion, semantic analysis, or summarization should not require keeping a user-facing request open unnecessarily if later architecture determines asynchronous processing is more appropriate.

Initial operational timeouts/cadence are defined in `20-calibration-and-operational-defaults.md`; interactive performance remains a quality goal rather than a public SLA.

---

## NFR-007 — Cost awareness

The system should avoid unnecessary external or GenAI processing cost.

Where feasible, deterministic filtering and prioritization should happen before expensive semantic or generative operations.

The implementation should allow future measurement of high-cost operations.

---

## NFR-008 — Security

Secrets, credentials, and external-service keys must not be committed to source control.

The system should follow secure configuration practices appropriate to the selected runtime and deployment environment.

Authentication remains intentionally minimal at the domain level because the MVP is single-user; the reference cloud protection is defined in `15-security-observability.md`.

---

## NFR-009 — Data traceability

Generated summaries and relevance explanations should remain traceable to source material and processing metadata sufficient to investigate incorrect behavior.

---

## NFR-010 — Portability of product logic

Core product decisions and domain rules should not be inseparably tied to one specific cloud vendor unless a documented trade-off explicitly justifies the coupling.

This does not prohibit choosing Azure or any other platform later.

---

## NFR-011 — Explainability of recommendations

The product should provide a human-readable explanation of why an update was selected rather than exposing only an opaque score.

---

## NFR-012 — Graceful degradation

If a semantic or GenAI capability is temporarily unavailable, the system should fail predictably and preserve recoverable work rather than silently corrupting selection state.

The exact degraded behavior is to be defined during architecture/spec refinement.

---

## NFR-013 — Scope discipline

The implementation must not introduce feed-like browsing, notification pressure, or additional content surfaces that contradict the product's information-reduction goal.
