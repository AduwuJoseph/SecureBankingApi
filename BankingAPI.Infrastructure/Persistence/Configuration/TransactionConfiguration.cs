using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BankingAPI.Domain.Entities;

namespace BankingAPI.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.Property(t => t.Amount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.TransactionType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.FailureReason)
            .HasMaxLength(500);

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(255);

        builder.HasIndex(t => t.SenderId);
        builder.HasIndex(t => t.RecipientId);
        builder.HasIndex(t => t.Timestamp);
        builder.HasIndex(t => t.TransactionReference).IsUnique();
        builder.HasIndex(t => t.IdempotencyKey).IsUnique();

        builder.HasOne(t => t.Sender)
            .WithMany(u => u.SentTransactions)
            .HasForeignKey(t => t.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Recipient)
            .WithMany(u => u.ReceivedTransactions)
            .HasForeignKey(t => t.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}