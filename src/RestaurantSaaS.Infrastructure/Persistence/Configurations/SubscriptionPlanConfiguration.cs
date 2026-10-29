
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Subscriptions;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionPlanConfiguration
    : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans", table =>
        {
            table.HasCheckConstraint(
                "CK_SubscriptionPlans_Price",
                "[Price] >= 0");

            table.HasCheckConstraint(
                "CK_SubscriptionPlans_MaxStaff",
                "[MaxStaff] >= 0");

            table.HasCheckConstraint(
                "CK_SubscriptionPlans_MaxMenuItems",
                "[MaxMenuItems] >= 0");

            table.HasCheckConstraint(
                "CK_SubscriptionPlans_MaxOrdersPerMonth",
                "[MaxOrdersPerMonth] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(p => p.BillingInterval)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.MaxStaff)
            .IsRequired();

        builder.Property(p => p.MaxMenuItems)
            .IsRequired();

        builder.Property(p => p.MaxOrdersPerMonth)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt);

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("UX_SubscriptionPlans_Name");

        // A plan code groups the monthly and yearly billing variants of the
        // same plan (for example `starter` appears once for Monthly and once
        // for Yearly). Uniqueness is therefore per (code, interval), not per
        // code alone, so the monthly and yearly variants can coexist while
        // duplicates of the same code+interval are rejected.
        builder.HasIndex(p => new { p.Code, p.BillingInterval })
            .IsUnique()
            .HasDatabaseName("UX_SubscriptionPlans_Code_BillingInterval");

        builder.HasIndex(p => new
        {
            p.IsActive,
            p.BillingInterval,
            p.Name
        })
            .HasDatabaseName(
                "IX_SubscriptionPlans_IsActive_BillingInterval_Name");
    }
}