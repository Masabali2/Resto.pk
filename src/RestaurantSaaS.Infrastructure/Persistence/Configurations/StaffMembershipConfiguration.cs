
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Tenants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class StaffMembershipConfiguration
    : IEntityTypeConfiguration<StaffMembership>
{
    public void Configure(EntityTypeBuilder<StaffMembership> builder)
    {
        builder.ToTable("StaffMemberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId)
            .IsRequired();

        builder.Property(m => m.UserId)
            .IsRequired();

        builder.Property(m => m.RoleId)
            .IsRequired();

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.JoinedAt)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.UpdatedAt);

        builder.HasIndex(m => new { m.TenantId, m.UserId })
            .IsUnique()
            .HasDatabaseName("UX_StaffMemberships_TenantId_UserId");

        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("IX_StaffMemberships_UserId");

        builder.HasIndex(m => m.RoleId)
            .HasDatabaseName("IX_StaffMemberships_RoleId");

        builder.HasIndex(m => new
        {
            m.TenantId,
            m.Status,
            m.JoinedAt
        })
            .HasDatabaseName(
                "IX_StaffMemberships_TenantId_Status_JoinedAt");

        builder.HasOne(m => m.Tenant)
            .WithMany(t => t.StaffMemberships)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Role)
            .WithMany()
            .HasForeignKey(m => m.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}