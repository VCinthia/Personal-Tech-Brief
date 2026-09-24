using PersonalTechBrief.Application.Evaluation;

namespace PersonalTechBrief.Web.Evaluation;

/// <summary>Public REST for the read-only FR-016 evaluation summary (§12).</summary>
public static class EvaluationEndpoints
{
    private const string EvaluationRoute = "/api/v1/evaluation";

    public static IEndpointRouteBuilder MapEvaluationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(EvaluationRoute);

        group.MapGet(
                "/summary",
                async (IEvaluationService service, CancellationToken cancellationToken) =>
                {
                    var summary = await service.GetSummaryAsync(cancellationToken);
                    return Results.Ok(EvaluationSummaryResponse.From(summary));
                })
            .WithName("GetEvaluationSummary")
            .Produces<EvaluationSummaryResponse>(StatusCodes.Status200OK);

        return endpoints;
    }
}
