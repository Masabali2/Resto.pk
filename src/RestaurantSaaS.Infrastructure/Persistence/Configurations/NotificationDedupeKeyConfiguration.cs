using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Notifications;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class NotificationDedupeKeyConfiguration
    : IEntityTypeConfiguration<NotificationDedupeKey>
{
    public void Configure(EntityTypeBuilder<NotificationDedupeKey> builder)
    {
        builder.ToTable("NotificationDedupeKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.EventKey)
            .IsRequired()
            .HasMaxLength(NotificationDedupeKey.MaxKeyLength);

        // One row per logical event: concurrent/retried producers collide on
        // this index and the duplicate is suppressed.
        builder.HasIndex(k => k.EventKey)
            .IsUnique()
            .HasDatabaseName("UX_NotificationDedupeKeys_EventKey");
    }
}
