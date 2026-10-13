
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Restaurants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class RestaurantAssetConfiguration
    : IEntityTypeConfiguration<RestaurantAsset>
{
    public void Configure(EntityTypeBuilder<RestaurantAsset> builder)
    {
        builder.ToTable("RestaurantAssets");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.RestaurantId)
            .IsRequired();

        builder.Property(a => a.FileId)
            .IsRequired();

        builder.Property(a => a.AssetType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt);

        builder.HasIndex(a => new
        {
            a.RestaurantId,
            a.AssetType,
            a.IsActive
        })
            .HasDatabaseName(
                "IX_RestaurantAssets_RestaurantId_AssetType_IsActive");

        builder.HasIndex(a => a.FileId)
            .HasDatabaseName("IX_RestaurantAssets_FileId");

        builder.HasOne(a => a.Restaurant)
            .WithMany(r => r.Assets)
            .HasForeignKey(a => a.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.File)
            .WithMany()
            .HasForeignKey(a => a.FileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}