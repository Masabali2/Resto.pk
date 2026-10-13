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

        builder.Property(m => m.Name).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(1000);

        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => new { m.RestaurantId, m.Status });

        // TENANT INTEGRITY (requirement #11): composite FK
        // (TenantId, RestaurantId) -> Restaurants(TenantId, Id).
        // The database rejects a menu that claims Tenant A but references a
        // restaurant owned by Tenant B — impossible even if app code has a bug.
        builder.HasOne(m => m.Restaurant)
            .WithMany()
            .HasForeignKey(m => new { m.TenantId, m.RestaurantId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
