using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.Web.Sources;

public static class SourceEndpoints
{
    private const string SourcesRoute = "/api/v1/sources";

    public static IEndpointRouteBuilder MapSourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(SourcesRoute);

        group.MapGet(
                string.Empty,
                async (ISourceService service, CancellationToken cancellationToken) =>
                {
                    var sources = await service.ListAsync(cancellationToken);
                    return Results.Ok(sources.Select(SourceResponse.From));
                })
            .WithName("ListSources")
            .Produces<IEnumerable<SourceResponse>>(StatusCodes.Status200OK);

        group.MapPost(
                string.Empty,
                async (CreateSourceRequest request, ISourceService service, CancellationToken cancellationToken) =>
                {
                    var validationProblem = Validate(request.Name, request.FeedUrl);
                    if (validationProblem is not null)
                    {
                        return validationProblem;
                    }

                    try
                    {
                        var source = await service.CreateAsync(
                            new CreateSourceCommand(request.Name!, request.FeedUrl!),
                            cancellationToken);
                        var response = SourceResponse.From(source);
                        return Results.Created($"{SourcesRoute}/{response.Id}", response);
                    }
                    catch (SourceConflictException)
                    {
                        return DuplicateSourceProblem();
                    }
                    catch (SourceFeedValidationException)
                    {
                        return UnsupportedFeedProblem();
                    }
                    catch (ArgumentException exception)
                    {
                        return InvalidSourceProblem(exception.Message);
                    }
                })
            .WithName("CreateSource")
            .Produces<SourceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet(
                "/{id:guid}",
                async (Guid id, ISourceService service, CancellationToken cancellationToken) =>
                {
                    var source = await service.GetByIdAsync(id, cancellationToken);
                    return source is null ? NotFoundSourceProblem() : Results.Ok(SourceResponse.From(source));
                })
            .WithName("GetSource")
            .Produces<SourceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut(
                "/{id:guid}",
                async (Guid id, UpdateSourceRequest request, ISourceService service, CancellationToken cancellationToken) =>
                {
                    var validationProblem = Validate(request.Name, request.FeedUrl, request.IsEnabled);
                    if (validationProblem is not null)
                    {
                        return validationProblem;
                    }

                    try
                    {
                        var source = await service.UpdateAsync(
                            id,
                            new UpdateSourceCommand(request.Name!, request.FeedUrl!, request.IsEnabled!.Value),
                            cancellationToken);
                        return Results.Ok(SourceResponse.From(source));
                    }
                    catch (KeyNotFoundException)
                    {
                        return NotFoundSourceProblem();
                    }
                    catch (SourceConflictException)
                    {
                        return DuplicateSourceProblem();
                    }
                    catch (SourceFeedValidationException)
                    {
                        return UnsupportedFeedProblem();
                    }
                    catch (ArgumentException exception)
                    {
                        return InvalidSourceProblem(exception.Message);
                    }
                })
            .WithName("UpdateSource")
            .Produces<SourceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete(
                "/{id:guid}",
                async (Guid id, ISourceService service, CancellationToken cancellationToken) =>
                {
                    var disabled = await service.DisableAsync(id, cancellationToken);
                    return disabled ? Results.NoContent() : NotFoundSourceProblem();
                })
            .WithName("DeleteSource")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost(
                "/{id:guid}/enable",
                async (Guid id, ISourceService service, CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var enabled = await service.EnableAsync(id, cancellationToken);
                        return enabled ? Results.NoContent() : NotFoundSourceProblem();
                    }
                    catch (SourceConflictException)
                    {
                        return DuplicateSourceProblem();
                    }
                    catch (SourceFeedValidationException)
                    {
                        return UnsupportedFeedProblem();
                    }
                })
            .WithName("EnableSource")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost(
                "/{id:guid}/disable",
                async (Guid id, ISourceService service, CancellationToken cancellationToken) =>
                {
                    var disabled = await service.DisableAsync(id, cancellationToken);
                    return disabled ? Results.NoContent() : NotFoundSourceProblem();
                })
            .WithName("DisableSource")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult? Validate(string? name, string? feedUrl, bool? isEnabled = true)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("name", ["Name is required."]);
        }
        else if (name.Trim().Length > SourceName.MaxLength)
        {
            errors.Add("name", [$"Name must not exceed {SourceName.MaxLength} characters."]);
        }

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            errors.Add("feedUrl", ["FeedUrl is required."]);
        }
        else
        {
            try
            {
                SourceFeedUrl.Parse(feedUrl);
            }
            catch (ArgumentException)
            {
                errors.Add("feedUrl", ["FeedUrl must be a safe, well-formed absolute HTTP or HTTPS URL."]);
            }
        }

        if (isEnabled is null)
        {
            errors.Add("isEnabled", ["IsEnabled is required."]);
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static IResult DuplicateSourceProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "An enabled source with this feed URL already exists.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.10");

    private static IResult UnsupportedFeedProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "The feed URL could not be validated as a supported RSS or Atom feed.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.21");

    private static IResult InvalidSourceProblem(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The source request is invalid.",
            detail: detail,
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.1");

    private static IResult NotFoundSourceProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The requested source does not exist.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
}
