
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Websites;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class WebsiteConfigurationVersionConfiguration
    : IEntityTypeConfiguration<WebsiteConfiguration>
{
    public void Configure(EntityTypeBuilder<WebsiteConfiguration> builder)
    {
        builder.ToTable("WebsiteConfigurations", table =>
        {
            table.HasCheckConstraint(
                "CK_WebsiteConfigurations_Version",
                "[ConfigurationVersion] >= 1");

            table.HasCheckConstraint(
                "CK_WebsiteConfigurations_ValidJson",
                "ISJSON([ConfigurationJson]) = 1");

            table.HasCheckConstraint(
                "CK_WebsiteConfigurations_PublishState",
                "([IsDraft] = 1 AND [PublishedAt] IS NULL) OR " +
                "([IsDraft] = 0 AND [PublishedAt] IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.WebsiteId)
            .IsRequired();

        builder.Property(c => c.ConfigurationVersion)
            .IsRequired();

        builder.Property(c => c.IsDraft)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.ConfigurationJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(c => c.PublishedAt);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt);

        builder.HasIndex(c => new
        {
            c.WebsiteId,
            c.ConfigurationVersion
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_WebsiteConfigurations_WebsiteId_Version");

        builder.HasIndex(c => c.WebsiteId)
            .IsUnique()
            .HasFilter("[IsDraft] = 1")
            .HasDatabaseName(
                "UX_WebsiteConfigurations_OneDraftPerWebsite");

        builder.HasOne(c => c.Website)
            .WithMany(w => w.Configurations)
            .HasForeignKey(c => c.WebsiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}