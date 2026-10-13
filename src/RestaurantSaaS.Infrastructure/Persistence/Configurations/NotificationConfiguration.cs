using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Notifications;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.RelatedEntityType).HasMaxLength(100);

        // Inbox query path: unread-first per user, plus tenant-wide feed.
        builder.HasIndex(n => new { n.TenantId, n.UserId, n.IsRead });
        builder.HasIndex(n => new { n.TenantId, n.CreatedAt });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Cascade); // notifications are disposable data

        // UserId kept WITHOUT FK constraint: notification history must survive
        // user deletion; the id is validated when written (requirement #14).
    }
}
