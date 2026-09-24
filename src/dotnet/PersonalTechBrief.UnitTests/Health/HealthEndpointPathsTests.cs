using PersonalTechBrief.Web.Health;

namespace PersonalTechBrief.UnitTests.Health;

public class HealthEndpointPathsTests
{
    [Fact]
    public void Live_path_matches_the_bootstrap_probe_contract()
    {
        Assert.Equal("/health/live", HealthEndpointPaths.Live);
    }

    [Fact]
    public void Ready_path_matches_the_bootstrap_probe_contract()
    {
        Assert.Equal("/health/ready", HealthEndpointPaths.Ready);
    }
}
