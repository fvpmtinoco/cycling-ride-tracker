using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Contracts;
using Cycling.Rider.Tracking.Infrastructure.Database;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Infrastructure.Outbox;

internal sealed class OutboxProcessor(
    DatabaseContext databaseContext,
    IFileStore fileStore,
    IPublishEndpoint publishEndpoint) : IOutboxProcessor
{
    public async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var files = await databaseContext.TransactionFiles
            .FromSql($@"SELECT * FROM transaction_files
                WHERE processed = false
                ORDER BY created_at
                LIMIT 50
                FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        foreach (var file in files)
        {
            using var ms = new MemoryStream(file.FileContent);
            // StoreAsync is idempotent. In the case the database changes saves fail,
            // the batch will be reprocessed and the upload will not create other files
            await fileStore.StoreAsync(file.FileId, ms, file.ContentType ?? "text/plain", cancellationToken);
            file.Processed = true;
            file.ProcessedAt = DateTimeOffset.UtcNow;

            await publishEndpoint.Publish(
                new RideFileSaved(file.FileId, file.ProcessedAt.Value),
                cancellationToken);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // No catch needed — dispose rolls back if Commit wasn't reached, exception propagates naturally
    }
}
