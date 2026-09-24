namespace PersonalTechBrief.Infrastructure.Intelligence.Contracts;

/// <summary>
/// Request body for <c>POST /internal/v1/similarity</c>. Field names mirror the frozen
/// Intelligence API contract (camelCase on the wire); see docs/architecture/intelligence-api.md.
/// </summary>
public sealed record SimilarityRequest
{
    public required Guid CorrelationId { get; init; }

    public required SimilarityCandidate Candidate { get; init; }

    /// <summary>Bounded 0..100. Empty yields an empty <see cref="SimilarityResponse.Results"/> list.</summary>
    public required IReadOnlyList<SimilarityRepresentative> Representatives { get; init; }
}

public sealed record SimilarityCandidate
{
    public required string Text { get; init; }
}

public sealed record SimilarityRepresentative
{
    public required Guid UpdateId { get; init; }

    public required string Text { get; init; }
}

/// <summary>
/// Response body (200) for <c>POST /internal/v1/similarity</c>.
/// </summary>
public sealed record SimilarityResponse
{
    public required Guid CorrelationId { get; init; }

    /// <summary>Deterministic algorithm version, e.g. <c>similarity-1</c>.</summary>
    public required string AlgorithmVersion { get; init; }

    /// <summary>Preserves the request order of representatives, one entry each.</summary>
    public required IReadOnlyList<SimilarityResult> Results { get; init; }
}

public sealed record SimilarityResult
{
    public required Guid UpdateId { get; init; }

    /// <summary>Deterministic float in [0,1].</summary>
    public required double Similarity { get; init; }
}
