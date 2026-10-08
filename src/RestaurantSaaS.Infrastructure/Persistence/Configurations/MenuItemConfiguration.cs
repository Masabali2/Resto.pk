using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Menus;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).HasMaxLength(MenuItem.MaxNameLength).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(MenuItem.MaxDescriptionLength);

        // MONEY: server-controlled price with exact decimal precision.
        builder.Property(i => i.Price).HasPrecision(18, 4).IsRequired();
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        builder.HasIndex(i => new { i.MenuCategoryId, i.DisplayOrder });
        builder.HasIndex(i => new { i.MenuCategoryId, i.IsAvailable });

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.MenuCategoryId)
            // RESTRICT + soft delete: an item referenced by historical OrderItems
            // must never be physically cascaded away (requirement #14).
            .OnDelete(DeleteBehavior.Restrict);
    }
}
