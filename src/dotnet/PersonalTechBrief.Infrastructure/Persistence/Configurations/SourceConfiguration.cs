using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.Infrastructure.Persistence.Configurations;

public sealed class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("Sources");

        builder.HasKey(source => source.Id);

        builder.Property(source => source.Name)
            .HasMaxLength(SourceName.MaxLength)
            .IsRequired();

        builder.Property(source => source.FeedUrl)
            .HasMaxLength(SourceFeedUrl.MaxLength)
            .IsRequired();

        builder.Property(source => source.NormalizedFeedUrl)
            .HasMaxLength(SourceFeedUrl.MaxLength)
            .IsRequired();

        builder.Property(source => source.IsEnabled)
            .IsRequired();

        builder.Property(source => source.ETag)
            .HasMaxLength(512);

        builder.Property(source => source.LastModified)
            .HasColumnType("datetimeoffset");

        builder.Property(source => source.LastIngestionAtUtc)
            .HasColumnType("datetime2");

        builder.Property(source => source.LastIngestionStatus)
            .HasMaxLength(32);

        builder.Property(source => source.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(source => source.UpdatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(source => source.NormalizedFeedUrl)
            .HasDatabaseName("UX_Sources_NormalizedFeedUrl_Enabled")
            .IsUnique()
            .HasFilter("[IsEnabled] = 1");
    }
}
