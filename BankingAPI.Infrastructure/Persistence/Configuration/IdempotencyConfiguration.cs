using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BankingAPI.Domain.Entities;

namespace BankingAPI.Infrastructure.Persistence.Configurations;

public class IdempotencyLogConfiguration : IEntityTypeConfiguration<IdempotencyLog>
{
    public void Configure(EntityTypeBuilder<IdempotencyLog> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();

        builder.Property(i => i.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(i => i.IdempotencyKey).IsUnique();

        builder.Property(i => i.Endpoint)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(i => i.RequestHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(i => i.ResponseJson)
            .IsRequired()
            .HasColumnType("LONGTEXT");

        builder.Property(i => i.ExpiresAt)
            .IsRequired(false);
    }
}