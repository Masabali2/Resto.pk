
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Restaurants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class RestaurantConfiguration
    : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("Restaurants");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.Property(r => r.Phone)
            .HasMaxLength(30);

        builder.Property(r => r.Email)
            .HasMaxLength(256);

        builder.Property(r => r.AddressLine1)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.AddressLine2)
            .HasMaxLength(200);

        builder.Property(r => r.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Country)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.TimeZone)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.DeletedAt);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt);

        builder.HasAlternateKey(r => new { r.TenantId, r.Id })
            .HasName("AK_Restaurants_TenantId_Id");

        builder.HasIndex(r => new
        {
            r.TenantId,
            r.Status,
            r.IsDeleted
        })
            .HasDatabaseName(
                "IX_Restaurants_TenantId_Status_IsDeleted");

        builder.HasIndex(r => new
        {
            r.TenantId,
            r.CreatedAt
        })
            .HasDatabaseName(
                "IX_Restaurants_TenantId_CreatedAt");

        builder.HasOne(r => r.Tenant)
            .WithMany(t => t.Restaurants)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(r => r.Assets)
            .AutoInclude(false);
    }
}