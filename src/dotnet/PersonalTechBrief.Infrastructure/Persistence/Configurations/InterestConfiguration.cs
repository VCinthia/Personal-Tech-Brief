using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class InterestConfiguration : IEntityTypeConfiguration<Interest>
{
    public void Configure(EntityTypeBuilder<Interest> builder)
    {
        builder.ToTable("Interests");

        builder.HasKey(interest => interest.Id);

        builder.Property(interest => interest.Name)
            .HasMaxLength(InterestName.MaxLength)
            .IsRequired();

        builder.Property(interest => interest.NormalizedName)
            .HasMaxLength(InterestName.MaxLength)
            .IsRequired();

        builder.Property(interest => interest.Priority)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(interest => interest.IsActive)
            .IsRequired();

        builder.Property(interest => interest.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(interest => interest.UpdatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(interest => interest.NormalizedName)
            .HasDatabaseName("UX_Interests_NormalizedName_Active")
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
