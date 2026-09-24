using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Analysis;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class TechnologyUpdateConfiguration : IEntityTypeConfiguration<TechnologyUpdate>
{
    public void Configure(EntityTypeBuilder<TechnologyUpdate> builder)
    {
        builder.ToTable("TechnologyUpdates");
        builder.HasKey(update => update.Id);
        builder.Property(update => update.Id).ValueGeneratedNever();

        builder.Property(update => update.RepresentativeTitle)
            .HasMaxLength(TechnologyUpdate.RepresentativeTitleMaxLength)
            .IsRequired();
        builder.Property(update => update.PrimaryTopic)
            .HasMaxLength(TechnologyUpdate.PrimaryTopicMaxLength)
            .IsRequired();
        builder.Property(update => update.FirstObservedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(update => update.LastObservedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(update => update.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(update => update.CurrentRelevanceScore).IsRequired();
        builder.Property(update => update.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(update => update.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();

        builder.HasIndex(update => new { update.PrimaryTopic, update.LastObservedAtUtc })
            .HasDatabaseName("IX_TechnologyUpdates_PrimaryTopic_LastObservedAtUtc");
        builder.HasIndex(update => update.LastObservedAtUtc)
            .HasDatabaseName("IX_TechnologyUpdates_LastObservedAtUtc");
    }
}
