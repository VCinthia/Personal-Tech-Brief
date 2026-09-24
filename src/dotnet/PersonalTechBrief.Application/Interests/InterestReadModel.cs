using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Application.Interests;

public sealed record InterestReadModel(
    Guid Id,
    string Name,
    InterestPriority Priority,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
