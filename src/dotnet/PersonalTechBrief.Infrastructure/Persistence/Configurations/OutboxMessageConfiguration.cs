using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Messaging;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Destination)
            .HasMaxLength(OutboxMessage.DestinationMaxLength)
            .IsRequired();
        builder.Property(message => message.Payload)
            .IsRequired();
        builder.Property(message => message.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(message => message.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(message => message.DispatchedAtUtc).HasColumnType("datetime2");
        builder.Property(message => message.LastDispatchAttemptAtUtc).HasColumnType("datetime2");
        builder.Property(message => message.LastDispatchError).HasMaxLength(OutboxMessage.ErrorDetailMaxLength);

        builder.HasIndex(message => new { message.Status, message.CreatedAtUtc })
            .HasDatabaseName("IX_OutboxMessages_Status_CreatedAtUtc");
        builder.HasIndex(message => message.SourceItemId)
            .HasDatabaseName("UX_OutboxMessages_SourceItemId")
            .IsUnique();

        builder.HasOne<PersonalTechBrief.Domain.Ingestion.SourceItem>()
            .WithMany()
            .HasForeignKey(message => message.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PersonalTechBrief.Domain.Ingestion.IngestionRun>()
            .WithMany()
            .HasForeignKey(message => message.IngestionRunId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PersonalTechBrief.Domain.Sources.Source>()
            .WithMany()
            .HasForeignKey(message => message.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
