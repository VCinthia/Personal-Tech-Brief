using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Briefs;
using PersonalTechBrief.Domain.Briefs;

namespace PersonalTechBrief.Web.Briefs;

public static class BriefEndpoints
{
    private const string BriefsRoute = "/api/v1/briefs";
    private const int DefaultPageLimit = 20;
    private const int MaxPageLimit = 100;

    public static IEndpointRouteBuilder MapBriefEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(BriefsRoute);

        group.MapPost(
                string.Empty,
                async (
                    IBriefRepository repository,
                    IBriefGenerationQueue queue,
                    IOptions<BriefGenerationOptions> options,
                    TimeProvider timeProvider,
                    CancellationToken cancellationToken) =>
                {
                    var now = timeProvider.GetUtcNow().UtcDateTime;
                    var previousGeneratedAtUtc = await repository.GetLatestCompletedBriefGeneratedAtUtcAsync(cancellationToken);
                    var windowStartUtc = previousGeneratedAtUtc ?? now.AddDays(-options.Value.WindowLookbackDays);

                    var brief = Brief.Create(now, windowStartUtc, now, options.Value.GenerationVersion, Guid.NewGuid());
                    await repository.AddAsync(brief, cancellationToken);
                    await queue.EnqueueAsync(brief.Id, cancellationToken);

                    return Results.Accepted($"{BriefsRoute}/{brief.Id}", CreateBriefResponse.From(brief));
                })
            .WithName("CreateBrief")
            .Produces<CreateBriefResponse>(StatusCodes.Status202Accepted);

        group.MapGet(
                "/current",
                async (IBriefRepository repository, CancellationToken cancellationToken) =>
                {
                    var brief = await repository.GetCurrentAsync(cancellationToken);
                    return brief is null ? NoCurrentBriefProblem() : Results.Ok(BriefResponse.From(brief));
                })
            .WithName("GetCurrentBrief")
            .Produces<BriefResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet(
                string.Empty,
                async (string? cursor, int? limit, IBriefRepository repository, CancellationToken cancellationToken) =>
                {
                    var pageLimit = Math.Clamp(limit ?? DefaultPageLimit, 1, MaxPageLimit);
                    var page = await repository.ListAsync(cursor, pageLimit, cancellationToken);
                    return Results.Ok(BriefHistoryResponse.From(page));
                })
            .WithName("ListBriefs")
            .Produces<BriefHistoryResponse>(StatusCodes.Status200OK);

        group.MapGet(
                "/{id:guid}",
                async (Guid id, IBriefRepository repository, CancellationToken cancellationToken) =>
                {
                    var brief = await repository.GetByIdAsync(id, cancellationToken);
                    return brief is null ? NotFoundBriefProblem() : Results.Ok(BriefResponse.From(brief));
                })
            .WithName("GetBrief")
            .Produces<BriefResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult NoCurrentBriefProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "No brief has been generated yet.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");

    private static IResult NotFoundBriefProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The requested brief does not exist.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
}
