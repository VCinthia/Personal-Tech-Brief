using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class UpdateInterestMatchConfiguration : IEntityTypeConfiguration<UpdateInterestMatch>
{
    public void Configure(EntityTypeBuilder<UpdateInterestMatch> builder)
    {
        builder.ToTable("UpdateInterestMatches");
        builder.HasKey(match => new { match.TechnologyUpdateId, match.InterestId });

        builder.Property(match => match.MatchStrength).IsRequired();
        builder.Property(match => match.MatchedAtUtc).HasColumnType("datetime2").IsRequired();

        builder.HasOne<TechnologyUpdate>()
            .WithMany()
            .HasForeignKey(match => match.TechnologyUpdateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Interest>()
            .WithMany()
            .HasForeignKey(match => match.InterestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
