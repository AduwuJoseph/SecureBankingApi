using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BankingAPI.Domain.Entities;

namespace BankingAPI.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.UserId);

        builder.Property(a => a.Balance)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(a => a.LastUpdated)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Configure RowVersion as concurrency token
        builder.Property(a => a.RowVersion)
            .IsRequired()
            .IsRowVersion()
            .HasConversion<byte[]>();

        // Add index for concurrency
        builder.HasIndex(a => a.RowVersion);
    }
}