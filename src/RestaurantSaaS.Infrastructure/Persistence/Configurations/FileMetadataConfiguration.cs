
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Files;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class FileMetadataConfiguration
    : IEntityTypeConfiguration<FileMetadata>
{
    public void Configure(EntityTypeBuilder<FileMetadata> builder)
    {
        builder.ToTable("Files", table =>
        {
            table.HasCheckConstraint(
                "CK_Files_FileSize",
                "[FileSize] > 0 AND [FileSize] <= 104857600");

            table.HasCheckConstraint(
                "CK_Files_DeletedAt",
                "[DeletedAt] IS NULL OR [DeletedAt] IS NOT NULL");
        });

        builder.HasKey(f => f.Id);

        builder.Property(f => f.TenantId)
            .IsRequired(false);

        builder.Property(f => f.FileName)
            .IsRequired()
            .HasMaxLength(FileMetadata.MaxFileNameLength);

        builder.Property(f => f.StorageKey)
            .IsRequired()
            .HasMaxLength(FileMetadata.MaxStorageKeyLength);

        builder.Property(f => f.ContentType)
            .IsRequired()
            .HasMaxLength(FileMetadata.MaxContentTypeLength);

        builder.Property(f => f.FileSize)
            .IsRequired();

        builder.Property(f => f.StorageProvider)
            .IsRequired()
            .HasMaxLength(FileMetadata.MaxStorageProviderLength)
            .HasDefaultValue("local");

        builder.Property(f => f.Width).IsRequired(false);

        builder.Property(f => f.Height).IsRequired(false);

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.DeletedAt)
            .IsRequired(false);

        // Supports tenant-scoped file lookups.
        builder.HasIndex(f => f.TenantId)
            .HasDatabaseName("IX_Files_TenantId");

        // A storage key must identify exactly one file.
        builder.HasIndex(f => f.StorageKey)
            .IsUnique()
            .HasDatabaseName("UX_Files_StorageKey");
    }
}