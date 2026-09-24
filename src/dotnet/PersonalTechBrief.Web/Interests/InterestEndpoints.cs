using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Web.Interests;

public static class InterestEndpoints
{
    private const string InterestsRoute = "/api/v1/interests";

    public static IEndpointRouteBuilder MapInterestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(InterestsRoute);

        group.MapGet(
                string.Empty,
                async (IInterestService service, CancellationToken cancellationToken) =>
                {
                    var interests = await service.ListAsync(cancellationToken);
                    return Results.Ok(interests.Select(InterestResponse.From));
                })
            .WithName("ListInterests")
            .Produces<IEnumerable<InterestResponse>>(StatusCodes.Status200OK);

        group.MapPost(
                string.Empty,
                async (CreateInterestRequest request, IInterestService service, CancellationToken cancellationToken) =>
                {
                    var validationProblem = Validate(request.Name, request.Priority);
                    if (validationProblem is not null)
                    {
                        return validationProblem;
                    }

                    try
                    {
                        var interest = await service.CreateAsync(
                            new CreateInterestCommand(request.Name!, request.Priority!.Value),
                            cancellationToken);
                        var response = InterestResponse.From(interest);
                        return Results.Created($"{InterestsRoute}/{response.Id}", response);
                    }
                    catch (InterestConflictException)
                    {
                        return DuplicateInterestProblem();
                    }
                    catch (ArgumentException exception)
                    {
                        return InvalidInterestProblem(exception.Message);
                    }
                })
            .WithName("CreateInterest")
            .Produces<InterestResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet(
                "/{id:guid}",
                async (Guid id, IInterestService service, CancellationToken cancellationToken) =>
                {
                    var interest = await service.GetByIdAsync(id, cancellationToken);
                    return interest is null ? NotFoundInterestProblem() : Results.Ok(InterestResponse.From(interest));
                })
            .WithName("GetInterest")
            .Produces<InterestResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut(
                "/{id:guid}",
                async (Guid id, UpdateInterestRequest request, IInterestService service, CancellationToken cancellationToken) =>
                {
                    var validationProblem = Validate(request.Name, request.Priority, request.IsActive);
                    if (validationProblem is not null)
                    {
                        return validationProblem;
                    }

                    try
                    {
                        var interest = await service.UpdateAsync(
                            id,
                            new UpdateInterestCommand(request.Name!, request.Priority!.Value, request.IsActive!.Value),
                            cancellationToken);
                        return Results.Ok(InterestResponse.From(interest));
                    }
                    catch (KeyNotFoundException)
                    {
                        return NotFoundInterestProblem();
                    }
                    catch (InterestConflictException)
                    {
                        return DuplicateInterestProblem();
                    }
                    catch (ArgumentException exception)
                    {
                        return InvalidInterestProblem(exception.Message);
                    }
                })
            .WithName("UpdateInterest")
            .Produces<InterestResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete(
                "/{id:guid}",
                async (Guid id, IInterestService service, CancellationToken cancellationToken) =>
                {
                    var disabled = await service.DisableAsync(id, cancellationToken);
                    return disabled ? Results.NoContent() : NotFoundInterestProblem();
                })
            .WithName("DisableInterest")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult? Validate(string? name, InterestPriority? priority, bool? isActive = true)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("name", ["Name is required."]);
        }
        else if (name.Trim().Length > InterestName.MaxLength)
        {
            errors.Add("name", [$"Name must not exceed {InterestName.MaxLength} characters."]);
        }

        if (priority is null || !Enum.IsDefined(priority.Value))
        {
            errors.Add("priority", ["Priority must be high, medium, or low."]);
        }

        if (isActive is null)
        {
            errors.Add("isActive", ["IsActive is required."]);
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static IResult DuplicateInterestProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "An active interest with this name already exists.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.10");

    private static IResult InvalidInterestProblem(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The interest request is invalid.",
            detail: detail,
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.1");

    private static IResult NotFoundInterestProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The requested interest does not exist.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
}
