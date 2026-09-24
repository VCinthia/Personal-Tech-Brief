using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Web.Interests;

public sealed record CreateInterestRequest(string? Name, InterestPriority? Priority);

public sealed record UpdateInterestRequest(string? Name, InterestPriority? Priority, bool? IsActive);
