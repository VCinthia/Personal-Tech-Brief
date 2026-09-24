# Personal Tech Brief — MVP Acceptance Criteria

**Status:** Approved baseline

These criteria are written as product-level acceptance conditions. They are not yet broken into implementation stories.

## AC-001 — Interest configuration

Given the user creates an interest with a valid name and priority, when the change is saved, then the interest is available to subsequent relevance evaluation.

Given an existing interest, when its priority changes, then future relevance evaluation uses the new priority.

---

## AC-002 — Feed source validation

Given a valid RSS/Atom feed URL, when the user registers it, then the source can become active for periodic ingestion.

Given an unsupported or invalid feed, when the user attempts to register it, then the system does not silently treat it as a valid active source.

---

## AC-003 — Disabled source

Given a source is disabled, when a later automatic ingestion cycle runs, then no new items are intentionally retrieved from that source.

---

## AC-004 — Repeated ingestion

Given a previously ingested source item appears again in a later feed retrieval, when the ingestion cycle runs, then the system does not create an unintended duplicate item that later appears separately in the brief.

---

## AC-005 — Related coverage

Given multiple items clearly refer to the same underlying technology update, when grouping succeeds, then the user can receive one consolidated update with multiple source references rather than several independent brief entries.

---

## AC-006 — Maximum brief size

Given more than five candidates are considered relevant, when the brief is generated, then no more than five primary updates are included.

---

## AC-007 — No artificial filling

Given only two candidates meet the selection criteria, when the brief is generated, then the brief contains two items rather than filling the remaining positions with lower-value content.

---

## AC-008 — Empty brief

Given no candidate meets the selection criteria, when the brief is generated, then the result is a valid empty brief rather than an error or a forced set of recommendations.

---

## AC-009 — Explain relevance

Given an update is selected for the brief, when the user reviews it, then the update contains a human-readable explanation of why it may matter to the user.

---

## AC-010 — Source traceability

Given an update is displayed, when the user inspects its source references, then at least one original source can be identified.

Given a grouped update has several supporting source items, when source references are inspected, then the relevant supporting sources remain traceable.

---

## AC-011 — Manual URL behavior

Given the user submits a valid individual article URL, when the item is accepted, then it can enter the normal processing flow without its host automatically becoming a permanent subscribed source.

---

## AC-012 — Feedback

Given a displayed update, when the user marks it `Relevant` or `Not relevant`, then that feedback is persisted and remains attributable to the corresponding update.

---

## AC-013 — Save state

Given a displayed update, when the user saves it, then the saved state persists for later review.

---

## AC-014 — History boundary

Given previous briefs exist, when the user opens History, then previous briefs and their selected updates are available.

The history experience must not require exposing every ingested item as a browseable feed.

---

## AC-015 — Cost-aware processing principle

Given content can be rejected using deterministic rules before expensive GenAI processing, when the pipeline executes, then the design should allow rejection before unnecessary generation/summarization calls.

The exact measurable threshold will be defined after architecture and instrumentation are selected.

---

## AC-016 — Product intent preservation

Given a new source is added, when subsequent briefs are generated, then the existence of the additional source does not by itself increase the maximum number of visible updates.
