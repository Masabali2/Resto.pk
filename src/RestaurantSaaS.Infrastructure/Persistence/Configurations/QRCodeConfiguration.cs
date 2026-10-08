using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.QRCodes;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class QRCodeConfiguration : IEntityTypeConfiguration<QRCode>
{
    public void Configure(EntityTypeBuilder<QRCode> builder)
    {
        builder.ToTable("QRCodes");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Name).HasMaxLength(100).IsRequired();
        builder.Property(q => q.Code).HasMaxLength(QRCode.CodeLength).IsFixedLength().IsRequired();
        builder.Property(q => q.TargetUrl).HasMaxLength(500).IsRequired();

        // SECURITY: short-link code is globally unique (it routes public traffic).
        builder.HasIndex(q => q.Code).IsUnique();
        builder.HasIndex(q => new { q.TenantId, q.RestaurantId });

        // Composite FK pins the QR to a restaurant of the SAME tenant.
        builder.HasOne(q => q.Restaurant)
            .WithMany()
            .HasForeignKey(q => new { q.TenantId, q.RestaurantId })
            .OnDelete(DeleteBehavior.Cascade); // QR codes die with their restaurant
    }
}
