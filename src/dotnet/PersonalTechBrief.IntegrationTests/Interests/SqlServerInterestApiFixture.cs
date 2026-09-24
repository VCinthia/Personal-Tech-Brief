using System.Diagnostics;
using System.Security.Cryptography;
using Testcontainers.MsSql;
using Xunit.Sdk;

namespace PersonalTechBrief.IntegrationTests.Interests;

public sealed class SqlServerInterestApiFixture : IAsyncLifetime
{
    // Official Microsoft SQL Server 2022 CU26 Ubuntu 22.04 image, pinned to the MCR manifest digest.
    private const string SqlServerImage =
        "mcr.microsoft.com/mssql/server@sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89";

    private MsSqlContainer? container;
    private string? unavailableReason;

    public SqlServerInterestApiFactory? Factory { get; private set; }

    public async Task InitializeAsync()
    {
        if (!DockerCapability.IsAvailable(out var reason))
        {
            unavailableReason = reason;
            return;
        }

        container = new MsSqlBuilder(SqlServerImage)
            .WithPassword(CreatePassword())
            .WithDatabase("PersonalTechBrief")
            .WithCleanUp(true)
            .Build();
        await container.StartAsync();

        Factory = new SqlServerInterestApiFactory(container.GetConnectionString());
        await Factory.MigrateDatabaseAsync();
    }

    public void ThrowIfDockerIsUnavailable()
    {
        if (unavailableReason is not null)
        {
            throw SkipException.ForSkip(unavailableReason);
        }
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    private static string CreatePassword()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(24);
        return $"Tih!{Convert.ToHexString(randomBytes)}a1";
    }

    private static class DockerCapability
    {
        public static bool IsAvailable(out string reason)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "version --format {{.Server.Version}}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                if (process is null)
                {
                    reason = "Docker is not available, so the disposable SQL Server integration test is skipped.";
                    return false;
                }

                if (!process.WaitForExit(10_000))
                {
                    throw new InvalidOperationException("Docker did not respond to the capability check within 10 seconds.");
                }

                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(standardOutput))
                {
                    reason = $"Docker is unavailable, so the disposable SQL Server integration test is skipped: {standardError.Trim()}";
                    return false;
                }

                reason = string.Empty;
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                reason = "Docker is not installed, so the disposable SQL Server integration test is skipped.";
                return false;
            }
        }
    }
}
