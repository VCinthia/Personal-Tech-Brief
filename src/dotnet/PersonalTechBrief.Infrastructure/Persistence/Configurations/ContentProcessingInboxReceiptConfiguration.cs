using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Messaging;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class ContentProcessingInboxReceiptConfiguration : IEntityTypeConfiguration<ContentProcessingInboxReceipt>
{
    public void Configure(EntityTypeBuilder<ContentProcessingInboxReceipt> builder)
    {
        builder.ToTable("ContentProcessingInbox");
        builder.HasKey(receipt => receipt.SourceItemId);
        builder.Property(receipt => receipt.SourceItemId).ValueGeneratedNever();
        builder.Property(receipt => receipt.EnvelopeJson).IsRequired();
        builder.Property(receipt => receipt.ReceivedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(receipt => receipt.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(receipt => new { receipt.Status, receipt.ReceivedAtUtc, receipt.SourceItemId });
        builder.HasOne<SourceItem>().WithMany().HasForeignKey(receipt => receipt.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
