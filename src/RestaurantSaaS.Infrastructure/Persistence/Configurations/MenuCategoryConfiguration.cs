
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Menus;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class MenuCategoryConfiguration
    : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.ToTable("MenuCategories", table =>
        {
            table.HasCheckConstraint(
                "CK_MenuCategories_DisplayOrder",
                "[DisplayOrder] >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.MenuId)
            .IsRequired();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(MenuCategory.MaxNameLength);

        builder.Property(c => c.Description)
            .HasMaxLength(MenuCategory.MaxDescriptionLength);

        builder.Property(c => c.DisplayOrder)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAt);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt);

        builder.HasIndex(c => new
        {
            c.MenuId,
            c.IsDeleted,
            c.IsActive,
            c.DisplayOrder
        })
            .HasDatabaseName(
                "IX_MenuCategories_MenuId_IsDeleted_IsActive_DisplayOrder");

        builder.HasOne(c => c.Menu)
            .WithMany(m => m.Categories)
            .HasForeignKey(c => c.MenuId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Category)
            .HasForeignKey(i => i.MenuCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}