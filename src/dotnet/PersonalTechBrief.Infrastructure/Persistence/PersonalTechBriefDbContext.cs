using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Interactions;
using PersonalTechBrief.Domain.Interests;
using PersonalTechBrief.Domain.Messaging;
using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.Infrastructure.Persistence;

public sealed class PersonalTechBriefDbContext(DbContextOptions<PersonalTechBriefDbContext> options)
    : DbContext(options)
{
    public DbSet<Interest> Interests => Set<Interest>();

    public DbSet<Source> Sources => Set<Source>();

    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();

    public DbSet<SourceItem> SourceItems => Set<SourceItem>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ContentProcessingInboxReceipt> ContentProcessingInbox => Set<ContentProcessingInboxReceipt>();

    public DbSet<TechnologyUpdate> TechnologyUpdates => Set<TechnologyUpdate>();

    public DbSet<TechnologyUpdateSource> TechnologyUpdateSources => Set<TechnologyUpdateSource>();

    public DbSet<UpdateInterestMatch> UpdateInterestMatches => Set<UpdateInterestMatch>();

    public DbSet<Brief> Briefs => Set<Brief>();

    public DbSet<BriefItem> BriefItems => Set<BriefItem>();

    public DbSet<BriefItemSource> BriefItemSources => Set<BriefItemSource>();

    public DbSet<UserFeedback> UserFeedback => Set<UserFeedback>();

    public DbSet<SavedUpdate> SavedUpdates => Set<SavedUpdate>();

    public DbSet<SourceOpenEvent> SourceOpenEvents => Set<SourceOpenEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersonalTechBriefDbContext).Assembly);

        // Source path/query casing is origin-significant. Scheme and host are canonicalized before
        // persistence, so a binary key makes only those canonical components insensitive.
        if (Database.IsSqlServer())
        {
            modelBuilder.Entity<Source>()
                .Property(source => source.NormalizedFeedUrl)
                .UseCollation("Latin1_General_100_BIN2");

            modelBuilder.Entity<SourceItem>()
                .Property(item => item.ExternalId)
                .UseCollation("Latin1_General_100_BIN2");
            modelBuilder.Entity<SourceItem>()
                .Property(item => item.NormalizedUrl)
                .UseCollation("Latin1_General_100_BIN2");
        }
    }
}
