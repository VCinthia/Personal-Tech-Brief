# Personal Tech Brief — Business Rules

**Status:** Approved baseline

## BR-001 — Brief size

A brief may contain at most five primary updates.

The system must not add low-value items solely to fill available positions.

---

## BR-002 — Empty brief is valid

A brief with zero items is a valid outcome when no candidate meets the relevance threshold or selection criteria.

---

## BR-003 — Source count does not determine output count

Adding more sources must not automatically increase the number of visible updates.

All sources compete within the same selection process.

---

## BR-004 — One event may have many sources

Multiple source items referring to the same underlying technology event should, where detected, be represented as one user-visible update with multiple supporting sources.

---

## BR-005 — Interests are explicit

The MVP bases personalization primarily on user-configured interests and their priority.

The system must not claim that it has learned a sophisticated user profile unless such capability is explicitly implemented later.

---

## BR-006 — Interest priority values

Supported MVP interest priorities are:

- High
- Medium
- Low

---

## BR-007 — Permanent MVP sources

RSS/Atom is the supported permanent source mechanism for the MVP.

Other source adapters are deferred unless the product definition is explicitly revised.

---

## BR-008 — Manual URL is not a subscription

Submitting an individual URL does not create a permanent source subscription.

---

## BR-009 — Prioritize before summarize

Where feasible, the system should reduce the candidate set before performing expensive summarization or other high-cost GenAI operations.

---

## BR-010 — Deterministic rules remain deterministic

Explicit source state, date constraints, exact duplicate checks, configured limits, and other deterministic constraints should not depend on a generative model when conventional logic is sufficient.

---

## BR-011 — User-facing relevance explanation

A selected update should provide a human-readable explanation of why it was considered relevant.

A raw numeric score alone is insufficient.

---

## BR-012 — No infinite feed

The primary product experience must not expose an endless stream of all ingested content.

---

## BR-013 — History contains selected value

MVP history is focused on previous briefs and previously selected updates rather than every ingested item.

---

## BR-014 — Feedback does not imply learning

Recording `Relevant`, `Not relevant`, `Save`, or source-open actions does not imply that an adaptive recommendation model exists.

---

## BR-015 — GitHub is a future signal provider

If GitHub Releases is introduced after the MVP, it must enter the same relevance pipeline and compete for the same limited brief capacity.

It must not create a separate mandatory feed.

---

## BR-016 — Post-MVP additions must preserve signal quality

A post-MVP feature or source integration should be justified by improved signal quality, improved relevance, improved traceability, or reduced user effort.

Feature growth that only increases content volume contradicts the product objective.
