
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Menus;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Menus");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId)
            .IsRequired();

        builder.Property(m => m.RestaurantId)
            .IsRequired();

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(m => m.Description)
            .HasMaxLength(1000);

        builder.Property(m => m.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.UpdatedAt);

        builder.HasAlternateKey(m => new { m.TenantId, m.Id })
            .HasName("AK_Menus_TenantId_Id");

        builder.HasIndex(m => new
        {
            m.TenantId,
            m.RestaurantId,
            m.Status,
            m.IsActive
        })
            .HasDatabaseName(
                "IX_Menus_TenantId_RestaurantId_Status_IsActive");

        builder.HasIndex(m => new
        {
            m.RestaurantId,
            m.Status
        })
            .HasDatabaseName(
                "IX_Menus_RestaurantId_Status");

        builder.HasOne(m => m.Restaurant)
            .WithMany()
            .HasForeignKey(m => new { m.TenantId, m.RestaurantId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}