# Personal Tech Brief — Processing Pipeline

**Status:** Approved implementation baseline

## Objective

Turn high-volume source items into a small number of trustworthy candidate updates while avoiding unnecessary GenAI cost and preventing retries from creating duplicates.

## Stage 1 — Retrieve

Input: enabled RSS/Atom source.  
Owner: .NET Ingestion Job.

Behavior:

- use bounded HTTP timeout;
- identify the application with an appropriate user agent;
- use `ETag` / `If-None-Match` and `Last-Modified` / `If-Modified-Since` when available;
- isolate failures by source;
- record an IngestionRun;
- apply retry only to transient failures.

## Stage 2 — Normalize

Owner: .NET Ingestion Job.

Canonicalize:

- source external ID;
- URL and canonical URL where known;
- title whitespace/casing for comparison;
- timestamps to UTC;
- short excerpt/content representation;
- stable hashes where useful.

Normalization must not discard original source traceability.

## Stage 3 — Deterministic deduplication

Owner: .NET.

Check in order where data is available:

1. source + external ID;
2. normalized/canonical URL;
3. content hash;
4. normalized title within a bounded time window.

A duplicate does not enqueue a fresh product item unnecessarily.

## Stage 4 — Queue

Persist first, then enqueue using a durable pattern that cannot silently lose a committed source item.

Preferred implementation: transactional outbox in SQL.

- ingestion transaction stores `SourceItem` plus `OutboxMessage`;
- dispatcher publishes outbox messages to Service Bus;
- successful publication marks the outbox record dispatched;
- retries use a stable message ID derived from the outbox/message identifier.

This prevents the classic DB-commit-success / message-publish-failure gap.

## Stage 5 — Eligibility filter

Owner: .NET Processing Worker.

Reject before expensive semantic work when deterministic rules are sufficient, for example:

- malformed/unsupported content;
- item already terminally processed;
- item outside the accepted processing horizon;
- duplicate discovered after queue retry/race;
- no active interests and therefore no meaningful personalization target.

Filtered is a valid processing outcome, not an exception.

## Stage 6 — Content intelligence

Owner: Python Intelligence API.

Perform:

- text cleanup/preprocessing;
- topic/interest analysis;
- impact signal extraction;
- deterministic similarity support;
- GenAI semantic analysis only where explicitly configured/needed.

Return typed output with algorithm/prompt/model version metadata.

## Stage 7 — Group

Owner: .NET domain/application logic using Python similarity signal.

Candidate groups are bounded by:

- recent time window;
- topic overlap;
- maximum comparison count.

If similarity exceeds the grouping threshold, link the source item to the existing TechnologyUpdate. Otherwise create a new TechnologyUpdate.

The threshold is configurable and must have unit/integration coverage around boundary behavior.

## Stage 8 — Relevance scoring

Owner: .NET.

Example initial scoring model (weights deliberately configurable, not contractual constants):

```text
interest priority/match   dominant positive weight
recency                   positive decaying weight
impact signal             positive weight
supporting-source count   small capped positive weight
quality/failure penalty   negative weight where appropriate
```

The implementation must log/retain component contributions for diagnostics without exposing an opaque numeric score as the user-facing explanation.

## Stage 9 — Candidate selection

For a brief window:

- exclude below-threshold candidates;
- rank remaining candidates;
- select max 5;
- never fill unused slots with below-threshold content;
- zero selections is valid.

## Stage 10 — Generate selected updates

Owner: Python Intelligence API + GenAI provider.

Only selected candidates receive final generated presentation text.

Required output:

- concise title;
- concise source-grounded summary;
- why-relevant explanation.

Validate schema before persistence. A generation failure must not invent fallback facts. The brief can remain generating/failed for that candidate or use a deterministic source-derived fallback defined in implementation specs.

## Stage 11 — Persist immutable brief snapshot

Owner: .NET.

Persist:

- selection rank;
- title/summary/why-relevant shown;
- source references;
- score snapshot;
- generation version/model/prompt metadata.

Later regrouping or re-analysis must not silently rewrite historical briefs.

## Retry and dead-letter rules

- consumer is at-least-once; application code is idempotent;
- transient dependency failures are retried with bounded backoff;
- validation/domain failures are not repeatedly retried;
- after configured delivery/retry limits, message moves to DLQ;
- item state records failure category/correlation ID;
- DLQ is diagnosable but no automated infinite replay exists.

## Cost-control rules

- no final LLM summary for every ingested item;
- deterministic dedupe/filter before semantic generation;
- batch similarity when possible;
- cap candidate comparisons;
- cache/reuse completed analysis by source-item/version;
- store model/prompt usage metadata sufficient to estimate later cost.
