using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Briefs;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class BriefConfiguration : IEntityTypeConfiguration<Brief>
{
    public void Configure(EntityTypeBuilder<Brief> builder)
    {
        builder.ToTable("Briefs");
        builder.HasKey(brief => brief.Id);
        builder.Property(brief => brief.Id).ValueGeneratedNever();

        builder.Property(brief => brief.GeneratedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(brief => brief.WindowStartUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(brief => brief.WindowEndUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(brief => brief.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(brief => brief.CandidateCount).IsRequired();
        builder.Property(brief => brief.SelectedCount).IsRequired();
        builder.Property(brief => brief.GenerationVersion)
            .HasMaxLength(Brief.GenerationVersionMaxLength)
            .IsRequired();
        builder.Property(brief => brief.CorrelationId).IsRequired();

        builder.HasIndex(brief => brief.GeneratedAtUtc)
            .HasDatabaseName("IX_Briefs_GeneratedAtUtc");

        builder.HasMany(brief => brief.Items)
            .WithOne()
            .HasForeignKey(item => item.BriefId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(brief => brief.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
