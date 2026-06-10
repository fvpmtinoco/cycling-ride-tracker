using Cycling.Rider.Tracking.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cycling.Rider.Tracking.Infrastructure.Idempotency;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.ToTable("idempotency_keys");
        builder.HasIndex(k => k.Key).IsUnique();
        builder.Property(k => k.Key).HasMaxLength(255).IsRequired();
        builder.Property(k => k.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.ResponseBody).IsRequired();
    }
}
