
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Audit;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration
    : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .IsRequired(false);

        builder.Property(a => a.UserId)
            .IsRequired(false);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(AuditLog.MaxActionLength);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(AuditLog.MaxEntityTypeLength);

        builder.Property(a => a.EntityId)
            .IsRequired(false);

        builder.Property(a => a.Description)
            .HasMaxLength(AuditLog.MaxDescriptionLength)
            .IsRequired(false);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(AuditLog.MaxIpAddressLength)
            .IsRequired(false);

        builder.Property(a => a.UserAgent)
            .HasMaxLength(AuditLog.MaxUserAgentLength)
            .IsRequired(false);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        // Tenant audit timeline, newest records first.
        builder.HasIndex(a => new { a.TenantId, a.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_TenantId_CreatedAt");

        // Audit history for a particular entity.
        builder.HasIndex(a => new
        {
            a.EntityType,
            a.EntityId,
            a.CreatedAt
        })
            .HasDatabaseName(
                "IX_AuditLogs_EntityType_EntityId_CreatedAt")
            .HasFilter("[EntityId] IS NOT NULL");

        // User activity timeline.
        builder.HasIndex(a => new { a.UserId, a.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_UserId_CreatedAt")
            .HasFilter("[UserId] IS NOT NULL");

        // Intentionally no foreign keys: audit history must survive
        // deletion of tenants, users, and referenced business entities.
    }
}