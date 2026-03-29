using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BankingAPI.Domain.Entities;

namespace BankingAPI.Infrastructure.Persistence.Configurations;

public class AccountLedgerConfiguration : IEntityTypeConfiguration<AccountLedger>
{
    public void Configure(EntityTypeBuilder<AccountLedger> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.TransactionReference)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.Amount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(l => l.PreviousBalance)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(l => l.NewBalance)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(l => l.Description)
            .HasMaxLength(500);

        builder.HasIndex(l => l.UserId);
        builder.HasIndex(l => l.TransactionReference);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Transaction)
            .WithMany(t => t.LedgerEntries)
            .HasForeignKey(l => l.TransactionReference)
            .HasPrincipalKey(t => t.Id.ToString());
    }
}