namespace PersonalTechBrief.Application.Evaluation;

/// <summary>
/// Read boundary for the FR-016 evaluation signals. Returns raw persisted counts and rows; the derived
/// aggregation is applied in <see cref="EvaluationService"/>.
/// </summary>
public interface IEvaluationRepository
{
    Task<EvaluationSignals> GetSignalsAsync(CancellationToken cancellationToken);
}
