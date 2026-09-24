using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Application.Interests;

public sealed record CreateInterestCommand(string Name, InterestPriority Priority);

public sealed record UpdateInterestCommand(string Name, InterestPriority Priority, bool IsActive);
