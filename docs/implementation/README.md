# Implementation Guide

This directory records the delivered MVP in implementation order. It is a set of
implementation notes, not a replacement for the approved product and architecture
specifications in `docs/specs/`.

| Delivery unit | Topic | Note |
| --- | --- | --- |
| Slice 1 | Explicit technology interests | [Slice 1 — Interests](./slice-1-interests.md) |
| Slice 2 | Validated RSS/Atom subscriptions | [Slice 2 — Sources](./slice-2-sources.md) |
| Slice 3 | Finite RSS ingestion and durable handoff | [Slice 3 — Ingestion and handoff](./slice-3-ingestion-and-handoff.md) |
| Slice 4 | Python Intelligence API and .NET client | [Slice 4 — Intelligence API](./slice-4-intelligence-api.md) |
| Slice 5 | Grouping and relevance | [Slice 5 — Grouping and relevance](./slice-5-grouping-and-relevance.md) |
| Slice 6 | Brief generation | [Slice 6 — Brief generation](./slice-6-brief-generation.md) |
| Slice 7 | Feedback, save, history, evaluation | [Slice 7 — Feedback, history, evaluation](./slice-7-feedback-history-evaluation.md) |
| Slice 8 | Manual article URL intake | [Slice 8 — Manual article intake](./slice-8-manual-article-intake.md) |
| UI redesign | Retro-editorial reskin of the web UI | [UI redesign](./ui-redesign.md) |

[Accepted delivery checkpoints](./delivery-checkpoints.md) records tags,
accepted commits, review dispositions and verification evidence.

Later MVP slices add their own focused notes here. The central product
constraint remains unchanged throughout: the product processes more material
than it displays, and it never becomes an infinite feed.
