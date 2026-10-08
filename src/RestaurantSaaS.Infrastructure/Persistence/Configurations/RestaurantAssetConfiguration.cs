using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Restaurants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class RestaurantAssetConfiguration : IEntityTypeConfiguration<RestaurantAsset>
{
    public void Configure(EntityTypeBuilder<RestaurantAsset> builder)
    {
        builder.ToTable("RestaurantAssets");
        builder.HasKey(a => a.Id);

        // One asset slot per type per restaurant (single Logo, single Favicon…).
        builder.HasIndex(a => new { a.RestaurantId, a.AssetType }).IsUnique();

        // Tenancy derives through Restaurant — no TenantId column (requirement #38).
        builder.HasOne(a => a.Restaurant)
            .WithMany(r => r.Assets)
            .HasForeignKey(a => a.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade); // assets die with their restaurant: not historical data

        builder.HasOne(a => a.File)
            .WithMany()
            .HasForeignKey(a => a.FileId)
            .OnDelete(DeleteBehavior.Restrict); // file metadata referenced elsewhere must not vanish
    }
}
