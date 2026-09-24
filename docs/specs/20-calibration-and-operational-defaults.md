# Personal Tech Brief — Initial Calibration and Operational Defaults

**Status:** Approved starting defaults; tunable through configuration after real usage

These values exist so implementation does not require the primary agent to invent product behavior. They are calibration defaults, not permanent product truths.

## Ingestion cadence

Cloud ingestion job default: every 4 hours.

Reasoning:

- technology news does not require minute-level freshness for this product;
- four-hour cadence is frequent enough for a daily brief;
- reduces unnecessary network/model processing;
- cadence remains configuration.

The user may trigger ingestion manually only through development/operations tooling initially; it is not required as an MVP UI feature.

## Brief creation

The MVP does not automatically push or schedule a user notification.

A new brief is generated explicitly (`POST /api/v1/briefs` / UI action).

Candidate window:

- primary window starts at the previous completed brief generation time;
- when no previous brief exists, inspect up to the previous 7 days;
- items already represented in a prior completed brief are not selected again unless a materially newer source item causes the underlying TechnologyUpdate to qualify as a new revision/change under later rules;
- MVP may initially choose the simpler rule: previously briefed TechnologyUpdates are excluded from subsequent briefs.

The simpler exclusion rule is the approved initial implementation.

## Maximum visible items

Hard maximum: 5.  
No minimum: 0.

## Relevance score — starting model

Score range is not user-facing.

### Interest component

Take the strongest matched active interest:

- High: 60 points × match strength;
- Medium: 40 points × match strength;
- Low: 20 points × match strength.

`match strength` is clamped to `[0,1]`.

Additional matched interests may contribute a small capped bonus of up to 10 points total so broad keyword overlap cannot dominate ranking.

### Recency component

Based on the newest supporting source timestamp:

- <= 24 hours: +20;
- >24 and <=72 hours: +12;
- >72 hours and <=7 days: +5;
- older than 7 days: +0 and normally outside the first-brief candidate window.

### Impact component

From content analysis:

- High: +20;
- Medium: +10;
- Low/Unknown: +0.

### Independent-source support component

Count distinct configured/manual source hosts supporting the grouped update:

- 1 source: +0;
- 2 sources: +4;
- 3+ sources: +7 maximum.

This is intentionally small so media repetition does not become importance by itself.

### Initial selection threshold

`55` points.

Rationale:

- strong High-priority interest match can qualify with modest supporting evidence;
- Medium requires stronger recency/impact/multiple-interest evidence;
- Low should appear only when the event is unusually significant/recent.

This threshold must be configuration and may be calibrated from Relevant/NotRelevant feedback.

## Grouping defaults

Candidate comparison is bounded to TechnologyUpdates that:

- have activity within the previous 7 days; and
- share at least one analyzed topic/interest or normalized keyword signal.

Python returns similarity in `[0,1]` with an algorithm version.

Initial merge threshold: `0.78`.

A value below threshold creates a new TechnologyUpdate. Equality qualifies for merge.

False merges are considered more harmful than missed merges for the initial MVP, so the threshold is intentionally conservative.

## LLM use defaults

### Per-item semantic analysis

Allowed only after deterministic duplicate/eligibility filtering.

The first implementation may use a model call to produce structured:

- interest/topic match strengths;
- impact level;
- normalized event descriptors/keywords.

The provider must be replaceable with deterministic fixtures in tests.

### Final generated presentation

Only the updates already selected for the brief receive the final generation call for:

- concise title;
- summary;
- why-relevant explanation.

## Retry defaults

Starting policy (configuration, not hardcoded across layers):

- HTTP feed/model transient calls: up to 3 attempts with exponential backoff + jitter;
- Service Bus consumer: rely on bounded broker deliveries plus application failure classification;
- terminal validation/domain failures: no transient retry loop;
- DLQ after configured broker delivery limit.

Exact Azure Service Bus delivery-count configuration is set in infrastructure code and documented with deployment values.

## Timeout defaults

Starting values:

- RSS/Atom HTTP retrieval: 15 seconds;
- Python internal API request from processor: 30 seconds for analysis, 60 seconds for final generation;
- LLM provider: 45 seconds per attempt.

Timeouts are configurable and traced.

## Content-size limits

Before model calls, bound source text to the content needed for the task.

Initial safety limits:

- feed/article text retained for analysis should be capped/configurable;
- manual URL download must have a strict byte limit;
- model prompt construction must apply its own character/token budget rather than forwarding arbitrary full pages.

The primary agent should choose concrete byte/token constants during the relevant slice only when library/provider constraints are known, document them, and keep them configurable.
