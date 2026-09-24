using PersonalTechBrief.Infrastructure.Intelligence.Contracts;

namespace PersonalTechBrief.Infrastructure.Intelligence;

/// <summary>
/// Typed client for the internal Intelligence API (ADR-002, FR-007/008/009). Wraps
/// <c>POST /internal/v1/items/analyze</c> and <c>POST /internal/v1/similarity</c>. All methods
/// propagate the supplied <see cref="CancellationToken"/>, apply the configured per-endpoint
/// timeout, and map non-success responses to <see cref="IntelligenceApiException"/>.
/// </summary>
public interface IIntelligenceApiClient
{
    /// <summary>Runs deterministic preprocessing plus semantic analysis for one item.</summary>
    /// <exception cref="IntelligenceApiException">The service returned a problem+json error (e.g. 400/422/502/503/504) or the request timed out.</exception>
    /// <exception cref="OperationCanceledException">The supplied <paramref name="cancellationToken"/> was cancelled.</exception>
    Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken);

    /// <summary>Computes deterministic similarity of a candidate against a bounded set of representatives.</summary>
    /// <exception cref="IntelligenceApiException">The service returned a problem+json error (e.g. 400/422) or the request timed out.</exception>
    /// <exception cref="OperationCanceledException">The supplied <paramref name="cancellationToken"/> was cancelled.</exception>
    Task<SimilarityResponse> ComputeSimilarityAsync(SimilarityRequest request, CancellationToken cancellationToken);

    /// <summary>Generates a concise title, source-grounded summary, and why-relevant explanation for one selected candidate (Slice 6).</summary>
    /// <exception cref="IntelligenceApiException">The service returned a problem+json error (e.g. 400/422/502/503/504) or the request timed out.</exception>
    /// <exception cref="OperationCanceledException">The supplied <paramref name="cancellationToken"/> was cancelled.</exception>
    Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken);
}
