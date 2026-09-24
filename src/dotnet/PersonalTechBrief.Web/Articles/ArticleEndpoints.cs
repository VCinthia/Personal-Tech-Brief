using PersonalTechBrief.Application.Articles;

namespace PersonalTechBrief.Web.Articles;

public static class ArticleEndpoints
{
    private const string ArticlesRoute = "/api/v1/articles";

    public static IEndpointRouteBuilder MapArticleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                ArticlesRoute,
                async (SubmitArticleRequest request, IManualArticleSubmissionService service, CancellationToken cancellationToken) =>
                {
                    var result = await service.SubmitAsync(request.Url, cancellationToken);
                    return result.Outcome switch
                    {
                        ManualArticleSubmissionOutcome.Queued => Results.Accepted(
                            $"{ArticlesRoute}/{result.SourceItemId}",
                            new SubmitArticleResponse(result.SourceItemId, "queued")),
                        ManualArticleSubmissionOutcome.Duplicate => Results.Accepted(
                            result.SourceItemId is { } id ? $"{ArticlesRoute}/{id}" : null,
                            new SubmitArticleResponse(result.SourceItemId, "duplicate")),
                        ManualArticleSubmissionOutcome.InvalidUrl => InvalidUrlProblem(),
                        _ => UnprocessableArticleProblem(),
                    };
                })
            .WithName("SubmitArticle")
            .Produces<SubmitArticleResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }

    private static IResult InvalidUrlProblem() =>
        Results.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["url"] = ["Enter a valid, absolute public http or https article URL."],
            });

    private static IResult UnprocessableArticleProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "The article URL could not be fetched or read.",
            detail: "The URL was accepted but the article could not be safely retrieved or its text could not be extracted.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.21");
}
