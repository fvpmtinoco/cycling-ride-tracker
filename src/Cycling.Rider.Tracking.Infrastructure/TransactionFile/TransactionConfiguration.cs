using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cycling.Rider.Tracking.Infrastructure.TransactionFile;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Domain.Outbox.TransactionFile>
{
    public void Configure(EntityTypeBuilder<Domain.Outbox.TransactionFile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("transaction_files");
    }
}
