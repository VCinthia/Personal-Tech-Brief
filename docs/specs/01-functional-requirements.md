# Personal Tech Brief — Functional Requirements

**Status:** Approved baseline  
**Depends on:** `00-product-definition.md`

This document translates the approved product definition into functional behavior. It is intentionally architecture-agnostic.

## FR-001 — Manage interests

The user must be able to:

- create an interest;
- edit an interest;
- disable or remove an interest;
- assign one of the supported priorities: High, Medium, Low;
- review the configured interests.

### Notes

- Interests are explicit user preferences.
- Automatic learned preferences are not required for the MVP.

---

## FR-002 — Manage RSS/Atom sources

The user must be able to:

- register an RSS/Atom source;
- edit source metadata when applicable;
- enable or disable a source;
- remove a source;
- inspect whether the source is active and whether its most recent ingestion succeeded.

The system must validate whether a configured source can be interpreted as a supported feed before treating it as active.

---

## FR-003 — Submit an individual article URL

The user may submit a single article URL as a one-off input.

Submitting a URL must not automatically register its website as a permanent source.

---

## FR-004 — Periodically ingest configured sources

The system must periodically retrieve new content from enabled sources.

The user should not need to trigger ingestion manually for normal operation.

The system must avoid treating the same exact source item as new on every ingestion cycle.

---

## FR-005 — Normalize source content

The system must transform incoming source items into a consistent internal representation sufficient for later deduplication, classification, grouping, relevance evaluation, prioritization, and summarization.

---

## FR-006 — Detect exact or obvious duplicates

The system must prevent obvious duplicates from being independently presented as separate updates.

Duplicate detection may use deterministic information such as canonical URLs, source identifiers, normalized titles, or equivalent stable information.

---

## FR-007 — Group semantically related content

The system should be able to group multiple source items when they refer to the same underlying technology update or event.

A grouped update may contain several supporting sources.

---

## FR-008 — Classify content by topic

The system must associate content or grouped updates with one or more topics relevant to configured interests when sufficient evidence exists.

---

## FR-009 — Evaluate relevance

The system must evaluate whether an update is sufficiently relevant to the user.

Relevance evaluation should consider available product signals such as:

- configured interests and priority;
- novelty;
- source authority where modeled;
- recency;
- magnitude or likely impact;
- redundancy;
- explicit user feedback when used by the MVP.

The evaluation method must be testable even if some semantic signals are provided by GenAI.

---

## FR-010 — Prioritize candidate updates

The system must rank or otherwise prioritize candidate updates before final brief generation.

Prioritization must support a deliberately small final result rather than trying to expose all relevant content.

---

## FR-011 — Generate a brief

The system must be able to produce a finite brief containing a maximum of five primary updates.

The brief may contain fewer than five items, including zero.

The system must not fill the brief with low-value content merely to reach the maximum size.

---

## FR-012 — Explain each selected update

For each selected update, the system should provide enough information for the user to understand:

- what happened;
- which topic it belongs to;
- why it may matter to the user;
- when it occurred;
- which sources support it;
- how to open the original source material.

---

## FR-013 — Track item state

The user must be able to assign or produce the following states/actions for a selected update:

- Read/Pending state;
- Save;
- Dismiss where applicable;
- Relevant feedback;
- Not relevant feedback.

Exact UI semantics remain to be designed.

---

## FR-014 — View current brief

The user must be able to view the latest generated brief.

The main experience must not behave as an infinite feed of all ingested content.

---

## FR-015 — View brief history

The user must be able to access previous briefs and the updates that were selected for them.

The MVP is not required to expose a complete browseable archive of every ingested item.

---

## FR-016 — Record basic evaluation signals

The system should retain enough information to evaluate MVP effectiveness, including where feasible:

- number of items ingested;
- number of items selected for briefs;
- grouped duplicates;
- relevant/not relevant feedback;
- source opens;
- saves;
- number of items per brief;
- source distribution.

These signals are intended for product evaluation, not engagement maximization.

---

## FR-017 — Preserve source attribution

Every selected update must retain traceability to at least one original source item.

When multiple items were grouped, the update should retain all relevant supporting source references available to the system.

---

## FR-018 — Support an empty brief

If no candidate satisfies the product's minimum relevance conditions, the system must be able to generate or present an empty brief as a valid outcome.

---

## Deferred functional requirements

The following are explicitly deferred and should not be silently implemented in the MVP:

- GitHub Releases ingestion;
- arbitrary scraping;
- semantic search;
- RAG;
- conversational chat;
- email or push notifications;
- mobile application;
- multi-user behavior;
- collaboration;
- social sharing;
- automated model training.
