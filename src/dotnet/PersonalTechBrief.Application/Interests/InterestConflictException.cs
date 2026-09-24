namespace PersonalTechBrief.Application.Interests;

public sealed class InterestConflictException : Exception
{
    public InterestConflictException(string normalizedName)
        : base($"An active interest named '{normalizedName}' already exists.")
    {
    }
}
