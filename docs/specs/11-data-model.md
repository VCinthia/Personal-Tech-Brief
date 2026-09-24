# Personal Tech Brief — Data Model

**Status:** Approved implementation baseline

## Principles

- Relational storage is the system of record.
- Product/domain data is owned by the .NET boundary.
- Brief history must preserve what the user actually saw.
- Source traceability must survive source disablement or later regrouping.
- Processing state must support retries without duplicate product objects.
- Raw LLM/provider responses are not domain objects; retain only what is needed for diagnostics/traceability.

## Core entities

### Interest

- `Id`
- `Name`
- `NormalizedName`
- `Priority` — High / Medium / Low
- `IsActive`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Constraint: active normalized names are unique.

### Source

- `Id`
- `Name`
- `FeedUrl`
- `NormalizedFeedUrl`
- `IsEnabled`
- `ETag` nullable
- `LastModified` nullable
- `LastIngestionAtUtc` nullable
- `LastIngestionStatus` nullable
- `CreatedAtUtc`
- `UpdatedAtUtc`

### IngestionRun

- `Id`
- `SourceId`
- `StartedAtUtc`
- `CompletedAtUtc` nullable
- `Status`
- `RetrievedItemCount`
- `NewItemCount`
- `ErrorCode` nullable
- `ErrorDetail` nullable/truncated
- `CorrelationId`

### SourceItem

Represents an original article/feed item. It is not itself the user-visible recommendation.

- `Id`
- `SourceId` nullable for manual submissions
- `OriginType` — Feed / ManualUrl
- `ExternalId` nullable
- `CanonicalUrl`
- `NormalizedUrl`
- `Title`
- `Excerpt` nullable
- `PublishedAtUtc` nullable
- `RetrievedAtUtc`
- `ContentHash` nullable
- `ProcessingStatus`
- `FailureCount`
- `LastFailureCode` nullable
- `CreatedAtUtc`

Indexes/constraints should support duplicate checks on source/external id and normalized URL where appropriate.

### ContentAnalysis

One current analysis result per source item for the active analysis version, while retaining enough version metadata for traceability.

- `Id`
- `SourceItemId`
- `AnalysisVersion`
- `MatchedTopicsJson` or normalized child rows
- `ImpactLevel`
- `ImpactConfidence` nullable
- `KeywordsJson` nullable
- `AnalyzedAtUtc`
- `ProviderKind`
- `ModelOrAlgorithmVersion`
- `PromptVersion` nullable
- `CorrelationId`

Implementation may normalize matched interests into a relation if querying requires it. Do not create complexity before query needs are known.

### TechnologyUpdate

Represents the user-visible underlying event/update that may be supported by several source items.

- `Id`
- `RepresentativeTitle`
- `PrimaryTopic`
- `FirstObservedAtUtc`
- `LastObservedAtUtc`
- `Status`
- `CurrentRelevanceScore`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### TechnologyUpdateSource

Many-to-many association:

- `TechnologyUpdateId`
- `SourceItemId`
- `SimilarityScore` nullable
- `LinkedAtUtc`

Unique pair constraint required.

### UpdateInterestMatch

- `TechnologyUpdateId`
- `InterestId`
- `MatchStrength`
- `MatchedAtUtc`

Supports explainable relevance without encoding all matching state into opaque JSON.

### Brief

- `Id`
- `GeneratedAtUtc`
- `WindowStartUtc`
- `WindowEndUtc`
- `Status` — Generating / Completed / Failed
- `CandidateCount`
- `SelectedCount`
- `GenerationVersion`
- `CorrelationId`

A completed brief with `SelectedCount = 0` is valid.

### BriefItem

Immutable snapshot of one selected update at brief-generation time.

- `Id`
- `BriefId`
- `TechnologyUpdateId`
- `Rank`
- `TitleSnapshot`
- `TopicSnapshot`
- `SummarySnapshot`
- `WhyRelevantSnapshot`
- `RelevanceScoreSnapshot`
- `GeneratedAtUtc`
- `PromptVersion`
- `ModelOrAlgorithmVersion`

Constraint: unique `(BriefId, Rank)` and `(BriefId, TechnologyUpdateId)`.

### UserFeedback

- `Id`
- `TechnologyUpdateId`
- `BriefItemId` nullable
- `FeedbackType` — Relevant / NotRelevant
- `CreatedAtUtc`

For MVP, latest explicit relevance feedback can supersede previous feedback for evaluation views; historical retention is allowed.

### SavedUpdate

- `TechnologyUpdateId`
- `SavedAtUtc`

Single-user MVP means no UserId is required.

### SourceOpenEvent

Lightweight evaluation event:

- `Id`
- `TechnologyUpdateId`
- `SourceItemId`
- `OpenedAtUtc`

This is product evaluation telemetry, not an engagement feed.

## Processing states

Suggested `SourceItem.ProcessingStatus` values:

```text
Pending
Queued
Processing
Processed
Filtered
FailedRetryable
FailedTerminal
```

State transitions must be explicit and covered by tests.

## Deletion policy

- Disabling/removing a configured Source must not erase historical SourceItems referenced by updates/briefs.
- Removing an Interest should normally soft-disable it so historical relevance traceability remains understandable.
- Brief and BriefItem history is immutable except for operational repair/migration scenarios.
