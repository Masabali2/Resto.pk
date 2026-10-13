using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Audit;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.IpAddress).HasMaxLength(45); // IPv6 length
        builder.Property(a => a.UserAgent).HasMaxLength(512);

        // Append-oriented: queried by tenant timeline and by entity drill-down.
        builder.HasIndex(a => new { a.TenantId, a.CreatedAt });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.UserId);

        // No FKs at all: audit rows must outlive tenants/users/entities.
        // Never cascade, never restrict — this table is write-once history.
    }
}
