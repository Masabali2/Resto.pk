
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Websites;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class WebsiteEntityConfiguration : IEntityTypeConfiguration<Website>
{
    public void Configure(EntityTypeBuilder<Website> builder)
    {
        builder.ToTable("Websites");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.TenantId)
            .IsRequired();

        builder.Property(w => w.RestaurantId)
            .IsRequired();

        builder.Property(w => w.WebsiteTemplateId)
            .IsRequired();

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(w => w.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(w => w.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.UpdatedAt);

        builder.HasAlternateKey(w => new { w.TenantId, w.Id })
            .HasName("AK_Websites_TenantId_Id");

        builder.HasIndex(w => new { w.TenantId, w.Slug })
            .IsUnique()
            .HasDatabaseName("UX_Websites_TenantId_Slug");

        builder.HasIndex(w => new { w.TenantId, w.Status })
            .HasDatabaseName("IX_Websites_TenantId_Status");

        builder.HasOne(w => w.Restaurant)
            .WithMany()
            .HasForeignKey(w => new { w.TenantId, w.RestaurantId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Template)
            .WithMany()
            .HasForeignKey(w => w.WebsiteTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}