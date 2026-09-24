using PersonalTechBrief.Application.Interactions;

namespace PersonalTechBrief.Web.Interactions;

/// <summary>
/// Public REST for the FR-013 interaction subset (§12): relevance feedback, saved state, and the
/// source-open telemetry signal. Read/Pending and Dismiss are out of scope for this slice.
/// </summary>
public static class InteractionEndpoints
{
    private const string UpdatesRoute = "/api/v1/updates";

    public static IEndpointRouteBuilder MapInteractionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(UpdatesRoute);

        group.MapPut(
                "/{id:guid}/feedback",
                async (Guid id, FeedbackRequest? request, IInteractionService service, CancellationToken cancellationToken) =>
                {
                    if (!FeedbackValue.TryParse(request?.Value, out var feedbackType))
                    {
                        return InvalidFeedbackValueProblem();
                    }

                    var outcome = await service.RecordFeedbackAsync(id, feedbackType, request!.BriefItemId, cancellationToken);
                    return outcome switch
                    {
                        RecordFeedbackOutcome.Recorded => Results.NoContent(),
                        RecordFeedbackOutcome.BriefItemNotFound => NotFoundProblem("The referenced brief item does not exist."),
                        _ => NotFoundProblem("The requested update does not exist."),
                    };
                })
            .WithName("RecordUpdateFeedback")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete(
                "/{id:guid}/feedback",
                async (Guid id, IInteractionService service, CancellationToken cancellationToken) =>
                {
                    await service.ClearFeedbackAsync(id, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("ClearUpdateFeedback")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPut(
                "/{id:guid}/saved",
                async (Guid id, IInteractionService service, CancellationToken cancellationToken) =>
                {
                    var saved = await service.SaveAsync(id, cancellationToken);
                    return saved ? Results.NoContent() : NotFoundProblem("The requested update does not exist.");
                })
            .WithName("SaveUpdate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete(
                "/{id:guid}/saved",
                async (Guid id, IInteractionService service, CancellationToken cancellationToken) =>
                {
                    await service.UnsaveAsync(id, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UnsaveUpdate")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost(
                "/{updateId:guid}/sources/{sourceItemId:guid}/open",
                async (Guid updateId, Guid sourceItemId, IInteractionService service, CancellationToken cancellationToken) =>
                {
                    var recorded = await service.RecordSourceOpenAsync(updateId, sourceItemId, cancellationToken);
                    return recorded
                        ? Results.Accepted()
                        : NotFoundProblem("The source item is not a supporting source of the update.");
                })
            .WithName("RecordSourceOpen")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult InvalidFeedbackValueProblem() =>
        Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["value"] = ["Value must be either \"relevant\" or \"notRelevant\"."],
        });

    private static IResult NotFoundProblem(string title) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: title,
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
}
