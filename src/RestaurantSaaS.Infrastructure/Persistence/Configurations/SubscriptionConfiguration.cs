
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Subscriptions;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration
    : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.SubscriptionPlanId)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.StartDate)
            .IsRequired();

        builder.Property(s => s.EndDate);

        builder.Property(s => s.AutoRenew)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt);

        builder.HasIndex(s => new
        {
            s.TenantId,
            s.Status,
            s.StartDate
        })
            .HasDatabaseName(
                "IX_Subscriptions_TenantId_Status_StartDate");

        builder.HasIndex(s => new
        {
            s.TenantId,
            s.StartDate
        })
            .HasDatabaseName(
                "IX_Subscriptions_TenantId_StartDate");

        builder.HasIndex(s => new
        {
            s.Status,
            s.EndDate
        })
            .HasDatabaseName(
                "IX_Subscriptions_Status_EndDate");

        builder.HasIndex(s => s.SubscriptionPlanId)
            .HasDatabaseName(
                "IX_Subscriptions_SubscriptionPlanId");

        builder.HasOne(s => s.Tenant)
            .WithMany(t => t.Subscriptions)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}