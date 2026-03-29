using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BankingAPI.Domain.Entities;

namespace BankingAPI.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Id).ValueGeneratedOnAdd();

        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(rt => rt.JwtId)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(rt => rt.JwtId);

        builder.Property(rt => rt.DeviceInfo)
            .HasMaxLength(100);

        builder.Property(rt => rt.IpAddress)
            .HasMaxLength(50);

        builder.Property(rt => rt.RevokedReason)
            .HasMaxLength(1000);

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(rt => rt.UserId);
        builder.HasIndex(rt => rt.ExpiryDate);
        builder.HasIndex(rt => new { rt.UserId, rt.IsRevoked });
    }
}