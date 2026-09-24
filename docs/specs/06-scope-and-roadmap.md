# Personal Tech Brief — Scope and Roadmap

**Status:** Approved baseline

## MVP scope

The MVP proves whether selected technology sources can be transformed into a very small, high-value brief using explicit interests, deduplication, grouping, relevance evaluation, prioritization, and concise explanations.

### In scope

- single-user product;
- explicit interests with High/Medium/Low priority;
- RSS/Atom source management;
- manual one-off article URL submission;
- automatic periodic ingestion;
- normalization;
- duplicate handling;
- grouping of related updates;
- topic classification;
- relevance evaluation;
- prioritization;
- brief generation with maximum five primary updates;
- concise summary and relevance explanation for selected updates;
- source traceability;
- Relevant / Not relevant feedback;
- Save state;
- current brief;
- previous brief history;
- basic product evaluation signals.

## Out of scope for MVP

- GitHub Releases;
- arbitrary scraping;
- LinkedIn/X/Reddit/YouTube integrations;
- email newsletter ingestion;
- outgoing notifications;
- mobile application;
- multi-user support;
- organizations and collaboration;
- semantic/vector search;
- RAG;
- chat interface;
- model training;
- sophisticated learned personalization;
- complex analytics dashboards;
- social features.

## Post-MVP roadmap principle

Post-MVP work should not be prioritized according to how many additional content sources can be connected.

Future work should be evaluated according to whether it:

- improves relevance;
- reduces false positives;
- improves deduplication/grouping;
- increases trust and explainability;
- reduces manual configuration effort;
- provides stronger signals without increasing unnecessary visible content;
- improves operational reliability or cost efficiency.

## Post-MVP hypothesis: GitHub Engineering Digest

GitHub Releases may become an additional signal provider when it materially improves detection of changes in technologies or dependencies the user follows.

It must use the shared prioritization pipeline and must not create a separate feed.

Success condition for this extension:

> GitHub-derived signals improve the quality or timeliness of selected updates without increasing the user's information burden merely because more data is available.

## Possible later hypotheses

These are ideas, not commitments:

- adaptive weighting based on explicit feedback;
- source-authority calibration;
- weekly mode in addition to daily/on-demand brief generation;
- semantic retrieval over previously selected/saved updates;
- limited RAG only if a concrete retrieval use case emerges;
- GitHub Releases provider;
- changelog/documentation provider;
- optional notification only for clearly exceptional, high-impact events.

Each hypothesis requires a product justification before entering implementation scope.
