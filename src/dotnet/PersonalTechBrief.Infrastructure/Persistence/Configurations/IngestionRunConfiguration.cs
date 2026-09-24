using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Ingestion;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    public void Configure(EntityTypeBuilder<IngestionRun> builder)
    {
        builder.ToTable("IngestionRuns");
        builder.HasKey(run => run.Id);

        builder.Property(run => run.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(run => run.StartedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(run => run.CompletedAtUtc).HasColumnType("datetime2");
        builder.Property(run => run.RetrievedItemCount).IsRequired();
        builder.Property(run => run.NewItemCount).IsRequired();
        builder.Property(run => run.ErrorCode).HasMaxLength(IngestionRun.ErrorCodeMaxLength);
        builder.Property(run => run.ErrorDetail).HasMaxLength(IngestionRun.ErrorDetailMaxLength);

        builder.HasIndex(run => new { run.SourceId, run.StartedAtUtc })
            .HasDatabaseName("IX_IngestionRuns_SourceId_StartedAtUtc");

        builder.HasOne<PersonalTechBrief.Domain.Sources.Source>()
            .WithMany()
            .HasForeignKey(run => run.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
