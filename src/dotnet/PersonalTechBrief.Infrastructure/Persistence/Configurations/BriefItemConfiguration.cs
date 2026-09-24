using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Briefs;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class BriefItemConfiguration : IEntityTypeConfiguration<BriefItem>
{
    public void Configure(EntityTypeBuilder<BriefItem> builder)
    {
        builder.ToTable("BriefItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.Property(item => item.BriefId).IsRequired();
        builder.Property(item => item.TechnologyUpdateId).IsRequired();
        builder.Property(item => item.Rank).IsRequired();
        builder.Property(item => item.TitleSnapshot).HasMaxLength(BriefItem.TitleSnapshotMaxLength).IsRequired();
        builder.Property(item => item.TopicSnapshot).HasMaxLength(BriefItem.TopicSnapshotMaxLength).IsRequired();
        builder.Property(item => item.SummarySnapshot).HasMaxLength(BriefItem.SummarySnapshotMaxLength).IsRequired();
        builder.Property(item => item.WhyRelevantSnapshot).HasMaxLength(BriefItem.WhyRelevantSnapshotMaxLength).IsRequired();
        builder.Property(item => item.RelevanceScoreSnapshot).IsRequired();
        builder.Property(item => item.GeneratedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.PromptVersion).HasMaxLength(BriefItem.VersionMaxLength).IsRequired();
        builder.Property(item => item.ModelOrAlgorithmVersion).HasMaxLength(BriefItem.VersionMaxLength).IsRequired();

        // Data model §11: unique (BriefId, Rank) and (BriefId, TechnologyUpdateId).
        builder.HasIndex(item => new { item.BriefId, item.Rank })
            .HasDatabaseName("UX_BriefItems_BriefId_Rank")
            .IsUnique();
        builder.HasIndex(item => new { item.BriefId, item.TechnologyUpdateId })
            .HasDatabaseName("UX_BriefItems_BriefId_TechnologyUpdateId")
            .IsUnique();

        // A historical snapshot references the update it captured, but re-analysis must never rewrite or
        // remove a completed brief, so the link is restrictive rather than cascading.
        builder.HasOne<TechnologyUpdate>()
            .WithMany()
            .HasForeignKey(item => item.TechnologyUpdateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Sources)
            .WithOne()
            .HasForeignKey(source => source.BriefItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Sources).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
