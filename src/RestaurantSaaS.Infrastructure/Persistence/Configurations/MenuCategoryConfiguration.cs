using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Menus;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.ToTable("MenuCategories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);

        // No TenantId here: tenancy derives Category -> Menu -> Restaurant -> Tenant.
        builder.HasIndex(c => new { c.MenuId, c.DisplayOrder });

        builder.HasOne(c => c.Menu)
            .WithMany(m => m.Categories)
            .HasForeignKey(c => c.MenuId)
            .OnDelete(DeleteBehavior.Cascade); // categories are part of the menu aggregate
    }
}
