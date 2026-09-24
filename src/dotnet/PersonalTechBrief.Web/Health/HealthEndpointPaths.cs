namespace PersonalTechBrief.Web.Health;

/// <summary>
/// Canonical local/bootstrap probe paths shared by the Web host and its tests.
/// </summary>
public static class HealthEndpointPaths
{
    public const string Live = "/health/live";

    public const string Ready = "/health/ready";
}
