
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Customers;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", table =>
        {
            table.HasCheckConstraint(
                "CK_Customers_IdentityRequired",
                """
                [Name] IS NOT NULL
                OR [Phone] IS NOT NULL
                OR [Email] IS NOT NULL
                """);
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.Name)
            .HasMaxLength(Customer.MaxNameLength);

        builder.Property(c => c.Phone)
            .HasMaxLength(Customer.MaxPhoneLength);

        builder.Property(c => c.Email)
            .HasMaxLength(Customer.MaxEmailLength);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAt)
            .IsRequired(false);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired(false);

        // Contact lookups during checkout or customer management.
        // Filtered indexes exclude deleted records.
        builder.HasIndex(c => new { c.TenantId, c.Phone })
            .HasDatabaseName("IX_Customers_TenantId_Phone")
            .HasFilter("[Phone] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(c => new { c.TenantId, c.Email })
            .HasDatabaseName("IX_Customers_TenantId_Email")
            .HasFilter("[Email] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}