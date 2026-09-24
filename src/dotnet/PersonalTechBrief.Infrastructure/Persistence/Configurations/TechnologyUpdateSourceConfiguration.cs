using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Ingestion;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class TechnologyUpdateSourceConfiguration : IEntityTypeConfiguration<TechnologyUpdateSource>
{
    public void Configure(EntityTypeBuilder<TechnologyUpdateSource> builder)
    {
        builder.ToTable("TechnologyUpdateSources");

        // The composite key enforces the required unique (TechnologyUpdateId, SourceItemId) pair.
        builder.HasKey(association => new { association.TechnologyUpdateId, association.SourceItemId });

        builder.Property(association => association.SimilarityScore);
        builder.Property(association => association.LinkedAtUtc).HasColumnType("datetime2").IsRequired();

        // A source item supports exactly one update in this slice; enforce it independently of the pair.
        builder.HasIndex(association => association.SourceItemId)
            .HasDatabaseName("UX_TechnologyUpdateSources_SourceItemId")
            .IsUnique();

        builder.HasOne<TechnologyUpdate>()
            .WithMany()
            .HasForeignKey(association => association.TechnologyUpdateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SourceItem>()
            .WithMany()
            .HasForeignKey(association => association.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
