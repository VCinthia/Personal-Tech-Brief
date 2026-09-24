# Personal Tech Brief — Use Cases

**Status:** Approved baseline

## UC-001 — Configure an interest

**Primary actor:** User

**Goal:** Define a technology topic the system should consider when evaluating relevance.

**Main flow:**

1. User opens Interests.
2. User creates or edits an interest.
3. User assigns High, Medium, or Low priority.
4. System validates the input.
5. System stores the preference.
6. Future relevance evaluation uses the updated preference.

---

## UC-002 — Add an RSS/Atom source

**Primary actor:** User

**Goal:** Add a trusted source to automatic ingestion.

**Main flow:**

1. User opens Sources.
2. User submits a feed URL.
3. System validates that the feed is supported.
4. System stores the source as enabled.
5. Future ingestion cycles include the source.

**Alternative flow:**

- If validation fails, the source is not activated and the user receives a useful error.

---

## UC-003 — Disable a source

**Primary actor:** User

**Goal:** Stop new automatic ingestion from a source without necessarily losing historical traceability.

**Main flow:**

1. User selects an enabled source.
2. User disables it.
3. System excludes it from future automatic ingestion.
4. Previously processed source references remain intact.

---

## UC-004 — Submit an individual article

**Primary actor:** User

**Goal:** Send a discovered article into the analysis pipeline without subscribing to its site.

**Main flow:**

1. User submits a URL.
2. System validates the input.
3. System obtains enough information to create an ingestible item.
4. Item enters the normal processing flow.
5. Website is not registered as a permanent source.

---

## UC-005 — Automatic ingestion cycle

**Primary actor:** System

**Goal:** Obtain new items from configured sources without manual intervention.

**Main flow:**

1. System identifies enabled sources due for ingestion.
2. System retrieves their current feed content.
3. System identifies items not already treated as existing content.
4. System normalizes new items.
5. New items enter downstream processing.
6. Processing state and failures remain diagnosable.

---

## UC-006 — Consolidate related source items

**Primary actor:** System

**Goal:** Avoid presenting the same underlying update multiple times.

**Main flow:**

1. System receives normalized candidate items.
2. System removes exact/obvious duplicates.
3. System evaluates remaining items for semantic relation.
4. Items representing the same event are grouped.
5. The resulting update retains references to supporting source items.

---

## UC-007 — Generate a brief

**Primary actor:** System

**Goal:** Produce a small set of high-value technology updates.

**Main flow:**

1. System identifies eligible processed updates.
2. System evaluates relevance using configured interests and available signals.
3. System prioritizes candidates.
4. System excludes candidates below selection criteria.
5. System selects up to five updates.
6. System generates the required concise explanation/summary for selected updates.
7. System stores/publishes the brief.

**Alternative flow:**

- No candidate is sufficiently relevant; the resulting brief contains zero items.

---

## UC-008 — Review current brief

**Primary actor:** User

**Goal:** Quickly understand the most relevant recent technology changes.

**Main flow:**

1. User opens Brief.
2. System displays the latest finite brief.
3. User reviews titles, topics, summaries, relevance explanations, dates, and source references.
4. User optionally opens original sources, saves an item, or gives feedback.

---

## UC-009 — Give relevance feedback

**Primary actor:** User

**Goal:** Record whether a recommendation was useful.

**Main flow:**

1. User selects `Relevant` or `Not relevant` on a brief item.
2. System records the feedback.
3. Feedback becomes available for product evaluation and later recommendation improvements where explicitly implemented.

---

## UC-010 — Save an update

**Primary actor:** User

**Goal:** Preserve a selected update for later reference.

**Main flow:**

1. User marks a brief item as saved.
2. System persists the saved state.
3. The state remains visible when the update is later reviewed.

---

## UC-011 — Review brief history

**Primary actor:** User

**Goal:** Revisit previously generated briefs and selected updates.

**Main flow:**

1. User opens History.
2. System displays previous briefs.
3. User opens a previous brief.
4. System displays the updates originally selected for that brief.

**Constraint:**

History must not become a general infinite feed of every ingested item.
