using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Domain.Ingestion;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class BriefItemSourceConfiguration : IEntityTypeConfiguration<BriefItemSource>
{
    public void Configure(EntityTypeBuilder<BriefItemSource> builder)
    {
        builder.ToTable("BriefItemSources");
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id).ValueGeneratedNever();

        builder.Property(source => source.BriefItemId).IsRequired();
        builder.Property(source => source.SourceItemId).IsRequired();
        builder.Property(source => source.TitleSnapshot).HasMaxLength(BriefItemSource.TitleSnapshotMaxLength).IsRequired();
        builder.Property(source => source.UrlSnapshot).HasMaxLength(BriefItemSource.UrlSnapshotMaxLength);
        builder.Property(source => source.PublishedAtUtc).HasColumnType("datetime2");

        builder.HasIndex(source => source.BriefItemId)
            .HasDatabaseName("IX_BriefItemSources_BriefItemId");

        // The supporting source item is retained for historical attribution even if the live source is
        // later disabled or removed, so the reference is restrictive rather than cascading.
        builder.HasOne<SourceItem>()
            .WithMany()
            .HasForeignKey(source => source.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
