using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class SavedUpdateConfiguration : IEntityTypeConfiguration<SavedUpdate>
{
    public void Configure(EntityTypeBuilder<SavedUpdate> builder)
    {
        builder.ToTable("SavedUpdates");

        // The update identifier is the key, so a save is naturally idempotent (one row per update).
        builder.HasKey(saved => saved.TechnologyUpdateId);
        builder.Property(saved => saved.TechnologyUpdateId).ValueGeneratedNever();
        builder.Property(saved => saved.SavedAtUtc).HasColumnType("datetime2").IsRequired();

        builder.HasOne<TechnologyUpdate>()
            .WithMany()
            .HasForeignKey(saved => saved.TechnologyUpdateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
