
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Identity;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations.Identity;

public sealed class EmailVerificationOtpConfiguration
    : IEntityTypeConfiguration<EmailVerificationOtp>
{
    public void Configure(
        EntityTypeBuilder<EmailVerificationOtp> builder)
    {
        builder.ToTable("EmailVerificationOtps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OtpHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.Property(x => x.FailedAttempts)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new
        {
            x.UserId,
            x.CreatedAtUtc
        });

        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}