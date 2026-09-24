using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Web.Interests;

public sealed record InterestResponse(
    Guid Id,
    string Name,
    InterestPriority Priority,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static InterestResponse From(InterestReadModel interest) =>
        new(
            interest.Id,
            interest.Name,
            interest.Priority,
            interest.IsActive,
            interest.CreatedAtUtc,
            interest.UpdatedAtUtc);
}
