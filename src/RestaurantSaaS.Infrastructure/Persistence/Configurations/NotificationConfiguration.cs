
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Notifications;
using RestaurantSaaS.Domain.Entities.Tenants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration
    : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.TenantId)
            .IsRequired();

        builder.Property(n => n.UserId)
            .IsRequired(false);

        builder.Property(n => n.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(n => n.RelatedEntityType)
            .HasMaxLength(100);

        builder.Property(n => n.RelatedEntityId)
            .IsRequired(false);

        builder.Property(n => n.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.ReadAt)
            .IsRequired(false);

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        // User inbox: retrieve notifications by tenant and user,
        // ordered by creation time.
        builder.HasIndex(n => new
        {
            n.TenantId,
            n.UserId,
            n.CreatedAt
        })
            .HasDatabaseName(
                "IX_Notifications_TenantId_UserId_CreatedAt");

        // Useful for tenant-wide notification feeds.
        builder.HasIndex(n => new
        {
            n.TenantId,
            n.CreatedAt
        })
            .HasDatabaseName(
                "IX_Notifications_TenantId_CreatedAt");

        // Tenant ownership.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Intentionally no User FK: notifications can retain the
        // original user ID after that user is deleted.
    }
}