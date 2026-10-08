using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Carts;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.UnitPrice).HasPrecision(18, 4);
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        // One line per menu item per cart; quantity merges into the existing line.
        builder.HasIndex(i => new { i.CartId, i.MenuItemId }).IsUnique();

        builder.HasOne(i => i.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade); // cart lines are transient, not historical

        // Cross-tenant safety net: MenuItemId -> MenuItems with Restrict.
        // The APPLICATION additionally verifies the item belongs to the cart's
        // tenant/restaurant (query filters + explicit check); this FK prevents
        // dangling references if an item is hard-purged.
        builder.HasOne<RestaurantSaaS.Domain.Entities.Menus.MenuItem>()
            .WithMany()
            .HasForeignKey(i => i.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
