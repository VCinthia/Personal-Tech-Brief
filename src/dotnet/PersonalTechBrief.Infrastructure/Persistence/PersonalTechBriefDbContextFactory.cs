using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalTechBrief.Infrastructure.Persistence;

public sealed class PersonalTechBriefDbContextFactory
    : IDesignTimeDbContextFactory<PersonalTechBriefDbContext>
{
    public PersonalTechBriefDbContext CreateDbContext(string[] args)
    {
        const string fallbackConnectionString =
            "Server=(localdb)\\mssqllocaldb;Database=PersonalTechBrief;Trusted_Connection=True;TrustServerCertificate=True";

        var connectionString = Environment.GetEnvironmentVariable("TIH_MIGRATIONS_CONNECTION_STRING")
            ?? fallbackConnectionString;

        var options = new DbContextOptionsBuilder<PersonalTechBriefDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new PersonalTechBriefDbContext(options);
    }
}
