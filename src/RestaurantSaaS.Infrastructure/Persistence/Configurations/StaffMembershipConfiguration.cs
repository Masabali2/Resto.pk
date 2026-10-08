using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Tenants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class StaffMembershipConfiguration : IEntityTypeConfiguration<StaffMembership>
{
    public void Configure(EntityTypeBuilder<StaffMembership> builder)
    {
        builder.ToTable("StaffMemberships");

        builder.HasKey(m => m.Id);

        // SECURITY INVARIANT: one membership per user per tenant.
        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => new { m.TenantId, m.Status });

        builder.HasOne(m => m.Tenant)
            .WithMany(t => t.StaffMemberships)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict); // never cascade-delete users out of existence

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
