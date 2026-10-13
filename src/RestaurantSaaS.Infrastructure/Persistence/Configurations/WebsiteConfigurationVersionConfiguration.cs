using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Websites;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class WebsiteConfigurationVersionConfiguration : IEntityTypeConfiguration<WebsiteConfiguration>
{
    public void Configure(EntityTypeBuilder<WebsiteConfiguration> builder)
    {
        builder.ToTable("WebsiteConfigurations");
        builder.HasKey(c => c.Id);

        // Flexible website settings live in JSON (explicitly allowed for this
        // entity only — NOT for relational business data).
        builder.Property(c => c.ConfigurationJson).HasMaxLength(WebsiteConfiguration.MaxJsonLength).IsRequired();

        // One draft per website; published versions are immutable history.
        builder.HasIndex(c => new { c.WebsiteId, c.ConfigurationVersion }).IsUnique();
        builder.HasIndex(c => new { c.WebsiteId, c.IsDraft });

        builder.HasOne(c => c.Website)
            .WithMany(w => w.Configurations)
            .HasForeignKey(c => c.WebsiteId)
            .OnDelete(DeleteBehavior.Cascade); // configuration versions belong to their website
    }
}
