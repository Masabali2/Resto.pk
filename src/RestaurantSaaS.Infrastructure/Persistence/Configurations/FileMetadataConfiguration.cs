using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Files;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
{
    public void Configure(EntityTypeBuilder<FileMetadata> builder)
    {
        builder.ToTable("Files");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName).HasMaxLength(255).IsRequired();
        // StorageKey is server-generated: tenant/{tenantId}/guid-filename.
        builder.Property(f => f.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(f => f.StorageProvider).HasMaxLength(50).IsRequired();

        // Nullable TenantId => platform-level files (templates, marketing).
        builder.HasIndex(f => f.TenantId);
        builder.HasIndex(f => f.StorageKey).IsUnique();
    }
}
