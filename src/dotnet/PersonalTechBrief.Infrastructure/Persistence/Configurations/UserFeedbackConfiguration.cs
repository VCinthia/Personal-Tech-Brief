using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class UserFeedbackConfiguration : IEntityTypeConfiguration<UserFeedback>
{
    public void Configure(EntityTypeBuilder<UserFeedback> builder)
    {
        builder.ToTable("UserFeedback");
        builder.HasKey(feedback => feedback.Id);
        builder.Property(feedback => feedback.Id).ValueGeneratedNever();

        builder.Property(feedback => feedback.TechnologyUpdateId).IsRequired();
        builder.Property(feedback => feedback.BriefItemId);
        builder.Property(feedback => feedback.FeedbackType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(feedback => feedback.CreatedAtUtc).HasColumnType("datetime2").IsRequired();

        // The current feedback for an update is its most recent row; this index serves that latest lookup.
        builder.HasIndex(feedback => new { feedback.TechnologyUpdateId, feedback.CreatedAtUtc })
            .HasDatabaseName("IX_UserFeedback_TechnologyUpdateId_CreatedAtUtc");

        // Feedback rows are their own history keyed to the live update and its brief item; a re-analysis or
        // brief regeneration must never rewrite them, so both links are restrictive rather than cascading.
        builder.HasOne<TechnologyUpdate>()
            .WithMany()
            .HasForeignKey(feedback => feedback.TechnologyUpdateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BriefItem>()
            .WithMany()
            .HasForeignKey(feedback => feedback.BriefItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
