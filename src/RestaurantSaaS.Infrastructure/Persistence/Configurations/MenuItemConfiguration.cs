
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Menus;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class MenuItemConfiguration
    : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems", table =>
        {
            table.HasCheckConstraint(
                "CK_MenuItems_Price",
                "[Price] >= 0");

            table.HasCheckConstraint(
                "CK_MenuItems_DisplayOrder",
                "[DisplayOrder] >= 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.MenuCategoryId)
            .IsRequired();

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(MenuItem.MaxNameLength);

        builder.Property(i => i.Description)
            .HasMaxLength(MenuItem.MaxDescriptionLength);

        builder.Property(i => i.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(i => i.ImageFileId);

        builder.Property(i => i.DisplayOrder)
            .IsRequired();

        builder.Property(i => i.IsAvailable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(i => i.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(i => i.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(i => i.DeletedAt);

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.UpdatedAt);

        builder.HasIndex(i => new
        {
            i.MenuCategoryId,
            i.IsDeleted,
            i.IsActive,
            i.IsAvailable,
            i.DisplayOrder
        })
            .HasDatabaseName(
                "IX_MenuItems_Category_Visibility_DisplayOrder");

        builder.HasIndex(i => new
        {
            i.MenuCategoryId,
            i.DisplayOrder
        })
            .HasDatabaseName(
                "IX_MenuItems_MenuCategoryId_DisplayOrder");

        builder.HasIndex(i => i.ImageFileId)
            .HasDatabaseName("IX_MenuItems_ImageFileId")
            .HasFilter("[ImageFileId] IS NOT NULL");

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.MenuCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RestaurantSaaS.Domain.Entities.Files.FileMetadata>()
            .WithMany()
            .HasForeignKey(i => i.ImageFileId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}