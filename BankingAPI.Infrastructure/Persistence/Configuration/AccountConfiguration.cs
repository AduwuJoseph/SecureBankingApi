using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BankingAPI.Domain.Entities;

namespace BankingAPI.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(a => a.AccountNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Balance)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(a => a.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(a => a.LastUpdated)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();
        // Configure RowVersion as concurrency token
        builder.Property(a => a.RowVersion)
            .IsRequired()
            .IsRowVersion()
            .HasConversion<byte[]>();

        // Relationships
        builder.HasMany(a => a.AccountLedgers)
            .WithOne(al => al.Account)
            .HasForeignKey(al => al.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.HasIndex(a => a.UserId);
    }
}