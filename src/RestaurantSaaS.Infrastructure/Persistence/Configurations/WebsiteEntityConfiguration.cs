using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Websites;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

/// <summary>
/// NOTE: named WebsiteEntityConfiguration to avoid clashing with the
/// WebsiteConfiguration ENTITY of the same conceptual area.
/// </summary>
public sealed class WebsiteEntityConfiguration : IEntityTypeConfiguration<Website>
{
    public void Configure(EntityTypeBuilder<Website> builder)
    {
        builder.ToTable("Websites");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name).HasMaxLength(150).IsRequired();
        builder.Property(w => w.Slug).HasMaxLength(100).IsRequired();

        // PUBLIC ROUTING + TENANT ISOLATION: slug unique WITHIN a tenant only.
        builder.HasIndex(w => new { w.TenantId, w.Slug }).IsUnique();
        builder.HasIndex(w => w.Status);

        // Composite FK keeps Website anchored to a restaurant of the SAME tenant.
        builder.HasOne(w => w.Restaurant)
            .WithMany()
            .HasForeignKey(w => new { w.TenantId, w.RestaurantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Template)
            .WithMany()
            .HasForeignKey(w => w.WebsiteTemplateId)
            .OnDelete(DeleteBehavior.Restrict); // published sites must not lose their template row
    }
}
