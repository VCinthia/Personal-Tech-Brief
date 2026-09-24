namespace PersonalTechBrief.Application.Evaluation;

/// <summary>Computes the read-only FR-016 product-evaluation summary from persisted signals.</summary>
public interface IEvaluationService
{
    Task<EvaluationSummary> GetSummaryAsync(CancellationToken cancellationToken);
}
