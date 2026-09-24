using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Web.Health;

/// <summary>
/// Confirms that the configured SQL Server persistence dependency can accept connections.
/// </summary>
public sealed class SqlServerReadinessHealthCheck(IServiceScopeFactory serviceScopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("The SQL Server persistence dependency is reachable.")
                : HealthCheckResult.Unhealthy("The SQL Server persistence dependency is unavailable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "The SQL Server persistence dependency is misconfigured or unavailable.",
                exception);
        }
    }
}
