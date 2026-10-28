
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Entities.Files;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class WebsiteTemplateConfiguration
    : IEntityTypeConfiguration<WebsiteTemplate>
{
    public void Configure(EntityTypeBuilder<WebsiteTemplate> builder)
    {
        builder.ToTable("WebsiteTemplates", table =>
        {
            table.HasCheckConstraint(
                "CK_WebsiteTemplates_ValidDesignJson",
                "[DesignConfigurationJson] IS NULL OR ISJSON([DesignConfigurationJson]) = 1");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.TemplateKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.PreviewImageFileId)
            .IsRequired(false);

        builder.Property(t => t.DesignConfigurationJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt);

        builder.HasIndex(t => t.TemplateKey)
            .IsUnique()
            .HasDatabaseName("UX_WebsiteTemplates_TemplateKey");

        builder.HasOne<FileMetadata>()
            .WithMany()
            .HasForeignKey(t => t.PreviewImageFileId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}