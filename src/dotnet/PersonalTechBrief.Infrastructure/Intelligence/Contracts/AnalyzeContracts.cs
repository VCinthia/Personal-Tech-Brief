namespace PersonalTechBrief.Infrastructure.Intelligence.Contracts;

/// <summary>
/// Request body for <c>POST /internal/v1/items/analyze</c>. Field names mirror the frozen
/// Intelligence API contract (camelCase on the wire); see docs/architecture/intelligence-api.md.
/// </summary>
public sealed record AnalyzeRequest
{
    public required Guid CorrelationId { get; init; }

    public required AnalyzeItem Item { get; init; }

    /// <summary>Bounded 0..100. Empty is valid (no interests to match).</summary>
    public required IReadOnlyList<CandidateInterest> CandidateInterests { get; init; }
}

public sealed record AnalyzeItem
{
    public required Guid SourceItemId { get; init; }

    public required string Title { get; init; }

    public string? Excerpt { get; init; }

    public string? Content { get; init; }

    /// <summary>BCP-47 language hint, or null.</summary>
    public string? Language { get; init; }
}

public sealed record CandidateInterest
{
    public required Guid InterestId { get; init; }

    public required string Name { get; init; }

    public required InterestPriority Priority { get; init; }
}

/// <summary>Interest priority. Serializes to the contract values <c>high|medium|low</c>.</summary>
public enum InterestPriority
{
    High,
    Medium,
    Low,
}

/// <summary>
/// Response body (200) for <c>POST /internal/v1/items/analyze</c>.
/// </summary>
public sealed record AnalyzeResponse
{
    public required Guid CorrelationId { get; init; }

    public required Guid SourceItemId { get; init; }

    /// <summary>Provider+prompt+post-processing version, e.g. <c>analyze-1</c>.</summary>
    public required string AnalyzerVersion { get; init; }

    public string? Language { get; init; }

    public required NormalizedFeatures Normalized { get; init; }

    public required IReadOnlyList<string> Topics { get; init; }

    /// <summary>At most one entry per supplied interest id; ids never invented.</summary>
    public required IReadOnlyList<InterestMatch> InterestMatches { get; init; }

    public required ImpactSignal Impact { get; init; }
}

public sealed record NormalizedFeatures
{
    public required IReadOnlyList<string> Keywords { get; init; }

    public required IReadOnlyList<string> EventDescriptors { get; init; }
}

public sealed record InterestMatch
{
    public required Guid InterestId { get; init; }

    /// <summary>Float in [0,1].</summary>
    public required double MatchStrength { get; init; }
}

public sealed record ImpactSignal
{
    public required ImpactLevel Level { get; init; }

    /// <summary>Float in [0,1].</summary>
    public required double Confidence { get; init; }
}

/// <summary>Impact level. Serializes to the contract values <c>high|medium|low|none</c>.</summary>
public enum ImpactLevel
{
    High,
    Medium,
    Low,
    None,
}
