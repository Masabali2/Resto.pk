using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Websites;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class WebsiteTemplateConfiguration : IEntityTypeConfiguration<WebsiteTemplate>
{
    public void Configure(EntityTypeBuilder<WebsiteTemplate> builder)
    {
        builder.ToTable("WebsiteTemplates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.TemplateKey).HasMaxLength(64).IsRequired();

        // Platform-owned catalogue keyed by a stable front-end lookup key.
        builder.HasIndex(t => t.TemplateKey).IsUnique();

        builder.HasOne<FileMetadata>()
            .WithMany()
            .HasForeignKey(t => t.PreviewImageFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
