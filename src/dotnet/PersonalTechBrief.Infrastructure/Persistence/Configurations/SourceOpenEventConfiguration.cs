using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class SourceOpenEventConfiguration : IEntityTypeConfiguration<SourceOpenEvent>
{
    public void Configure(EntityTypeBuilder<SourceOpenEvent> builder)
    {
        builder.ToTable("SourceOpenEvents");
        builder.HasKey(sourceOpen => sourceOpen.Id);
        builder.Property(sourceOpen => sourceOpen.Id).ValueGeneratedNever();

        builder.Property(sourceOpen => sourceOpen.TechnologyUpdateId).IsRequired();
        builder.Property(sourceOpen => sourceOpen.SourceItemId).IsRequired();
        builder.Property(sourceOpen => sourceOpen.OpenedAtUtc).HasColumnType("datetime2").IsRequired();

        builder.HasIndex(sourceOpen => sourceOpen.TechnologyUpdateId)
            .HasDatabaseName("IX_SourceOpenEvents_TechnologyUpdateId");

        // Append-only telemetry keyed to the live update and source item; never cascade from those rows.
        builder.HasOne<TechnologyUpdate>()
            .WithMany()
            .HasForeignKey(sourceOpen => sourceOpen.TechnologyUpdateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceItem>()
            .WithMany()
            .HasForeignKey(sourceOpen => sourceOpen.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
