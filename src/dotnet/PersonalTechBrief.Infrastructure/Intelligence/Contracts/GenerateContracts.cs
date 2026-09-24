namespace PersonalTechBrief.Infrastructure.Intelligence.Contracts;

/// <summary>
/// The frozen bounds of the generate request contract (docs/architecture/intelligence-api.md and
/// docs/contracts/intelligence). Domain maxima are wider than these, so the request builder clamps
/// each bounded field and caps each collection to these values before the provider call.
/// </summary>
public static class GenerateContractLimits
{
    public const int PrimaryTopicMaxLength = 200;
    public const int SourceTitleMaxLength = 512;
    public const int SourceExcerptMaxLength = 4000;
    public const int InterestNameMaxLength = 100;
    public const int MaxSources = 50;
    public const int MaxMatchedInterests = 100;
}

/// <summary>
/// Request body for <c>POST /internal/v1/updates/generate</c> (Slice 6). Field names mirror the frozen
/// generate contract exactly (camelCase on the wire); see docs/architecture/intelligence-api.md and
/// docs/contracts/intelligence/generate-request.json.
/// </summary>
public sealed record GenerateRequest
{
    public required Guid CorrelationId { get; init; }

    public required GenerateUpdate Update { get; init; }

    /// <summary>Bounded 0..100. Empty is valid (no interests matched).</summary>
    public required IReadOnlyList<GenerateMatchedInterest> MatchedInterests { get; init; }
}

public sealed record GenerateUpdate
{
    public required Guid TechnologyUpdateId { get; init; }

    public required string PrimaryTopic { get; init; }

    /// <summary>Bounded 1..50 supporting sources.</summary>
    public required IReadOnlyList<GenerateSource> Sources { get; init; }
}

public sealed record GenerateSource
{
    public required Guid SourceItemId { get; init; }

    public required string Title { get; init; }

    public string? Excerpt { get; init; }

    public string? SourceUrl { get; init; }

    public DateTime? PublishedAtUtc { get; init; }
}

public sealed record GenerateMatchedInterest
{
    public required Guid InterestId { get; init; }

    public required string Name { get; init; }

    public required InterestPriority Priority { get; init; }
}

/// <summary>
/// Response body (200) for <c>POST /internal/v1/updates/generate</c>. See
/// docs/contracts/intelligence/generate-response.json.
/// </summary>
public sealed record GenerateResponse
{
    public required Guid CorrelationId { get; init; }

    public required Guid TechnologyUpdateId { get; init; }

    /// <summary>Identifies provider+prompt+post-processing for traceability; persisted on the brief item.</summary>
    public required string GenerationVersion { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string WhyRelevant { get; init; }
}
