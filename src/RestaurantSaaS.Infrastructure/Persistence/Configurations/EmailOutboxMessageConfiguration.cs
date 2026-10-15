using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Notifications;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class EmailOutboxMessageConfiguration
    : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.RecipientEmail)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(m => m.Subject)
            .IsRequired()
            .HasMaxLength(EmailOutboxMessage.MaxSubjectLength);

        builder.Property(m => m.HtmlBody)
            .IsRequired()
            .HasMaxLength(EmailOutboxMessage.MaxBodyLength);

        builder.Property(m => m.EventName)
            .IsRequired()
            .HasMaxLength(EmailOutboxMessage.MaxEventNameLength);

        builder.Property(m => m.DedupeKey)
            .IsRequired()
            .HasMaxLength(EmailOutboxMessage.MaxDedupeKeyLength);

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.Attempts)
            .IsRequired();

        builder.Property(m => m.MaxAttempts)
            .IsRequired();

        builder.Property(m => m.NextAttemptAtUtc)
            .IsRequired(false);

        builder.Property(m => m.SentAtUtc)
            .IsRequired(false);

        builder.Property(m => m.LastError)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .IsRequired(false);

        // Idempotency: one durable record per logical event instance.
        builder.HasIndex(m => new { m.EventName, m.DedupeKey })
            .IsUnique()
            .HasDatabaseName("UX_EmailOutboxMessages_EventName_DedupeKey");

        // Worker polling: due pending messages, oldest first.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAtUtc })
            .HasDatabaseName("IX_EmailOutboxMessages_Status_NextAttemptAtUtc");
    }
}
