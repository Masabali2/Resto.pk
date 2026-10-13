
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

        builder.Property(q => q.TenantId)
            .IsRequired();

        builder.Property(q => q.RestaurantId)
            .IsRequired();

        builder.Property(q => q.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(q => q.Code)
            .IsRequired()
            .HasMaxLength(QRCode.CodeLength)
            .IsFixedLength();

        builder.Property(q => q.TargetUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(q => q.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(q => q.CreatedAt)
            .IsRequired();

        builder.Property(q => q.UpdatedAt);

        builder.HasIndex(q => q.Code)
            .IsUnique()
            .HasDatabaseName("UX_QRCodes_Code");

        builder.HasIndex(q => new { q.TenantId, q.RestaurantId })
            .HasDatabaseName("IX_QRCodes_TenantId_RestaurantId");

        builder.HasOne(q => q.Restaurant)
            .WithMany()
            .HasForeignKey(q => new { q.TenantId, q.RestaurantId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}