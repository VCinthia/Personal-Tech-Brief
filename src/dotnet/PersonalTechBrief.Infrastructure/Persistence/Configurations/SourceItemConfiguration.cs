using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Ingestion;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class SourceItemConfiguration : IEntityTypeConfiguration<SourceItem>
{
    public void Configure(EntityTypeBuilder<SourceItem> builder)
    {
        builder.ToTable("SourceItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.OriginType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(item => item.ExternalId)
            .HasMaxLength(SourceItemText.ExternalIdMaxLength);
        builder.Property(item => item.OriginalUrl)
            .HasMaxLength(SourceItemUrl.MaxLength);
        builder.Property(item => item.CanonicalUrl)
            .HasMaxLength(SourceItemUrl.MaxLength);
        builder.Property(item => item.NormalizedUrl)
            .HasMaxLength(SourceItemUrl.MaxLength);
        builder.Property(item => item.NormalizedUrlHash)
            .HasMaxLength(64);
        builder.Property(item => item.Title)
            .HasMaxLength(SourceItemText.TitleMaxLength)
            .IsRequired();
        builder.Property(item => item.NormalizedTitle)
            .HasMaxLength(SourceItemText.TitleMaxLength)
            .IsRequired();
        builder.Property(item => item.Excerpt)
            .HasMaxLength(SourceItemText.ExcerptMaxLength);
        builder.Property(item => item.PublishedAtUtc).HasColumnType("datetime2");
        builder.Property(item => item.RetrievedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.ContentHash)
            .HasMaxLength(SourceItemText.ContentHashMaxLength);
        builder.Property(item => item.ProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(item => item.LastFailureCode).HasMaxLength(128);
        builder.Property(item => item.CreatedAtUtc).HasColumnType("datetime2").IsRequired();

        builder.HasIndex(item => new { item.SourceId, item.ExternalId })
            .HasDatabaseName("UX_SourceItems_SourceId_ExternalId")
            .IsUnique()
            .HasFilter("[SourceId] IS NOT NULL AND [ExternalId] IS NOT NULL");
        builder.HasIndex(item => item.NormalizedUrlHash)
            .HasDatabaseName("IX_SourceItems_NormalizedUrlHash")
            .HasFilter("[NormalizedUrlHash] IS NOT NULL");
        // A source-less manual article (FR-003) has no source/external-id unique key, so the
        // normalized URL is its authoritative dedup key. This filtered unique index makes the
        // check-then-insert race safe: a concurrent resubmission of the same URL fails the insert
        // (caught as a duplicate) instead of double-inserting. It is a distinct, named index that
        // coexists with the non-unique lookup index above; feed items are excluded by the filter.
        builder.HasIndex([nameof(SourceItem.NormalizedUrlHash)], "UX_SourceItems_ManualUrl_NormalizedUrlHash")
            .IsUnique()
            .HasFilter("[OriginType] = 'ManualUrl' AND [NormalizedUrlHash] IS NOT NULL");
        builder.HasIndex(item => item.ContentHash)
            .HasDatabaseName("IX_SourceItems_ContentHash")
            .HasFilter("[ContentHash] IS NOT NULL");
        builder.HasIndex(item => new { item.NormalizedTitle, item.RetrievedAtUtc })
            .HasDatabaseName("IX_SourceItems_NormalizedTitle_RetrievedAtUtc");

        builder.HasOne<PersonalTechBrief.Domain.Sources.Source>()
            .WithMany()
            .HasForeignKey(item => item.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
