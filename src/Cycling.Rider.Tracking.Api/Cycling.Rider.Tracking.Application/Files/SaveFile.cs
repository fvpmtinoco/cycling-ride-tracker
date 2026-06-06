using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.SharedKernel;

namespace Cycling.Rider.Tracking.Application.Files;

public sealed record SaveFileCommand(Stream Content, string ContentType) : ICommand<FileLocation>;

public sealed class SaveFileCommandHandler(IDatabaseContext databaseContext, IFileStore fileStore) : ICommandHandler<SaveFileCommand, FileLocation>
{
    public async Task<Result<FileLocation>> HandleAsync(SaveFileCommand command, CancellationToken cancellationToken)
    {
        var entity = databaseContext.Rides.Add(new Domain.Rides.Ride
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
            RideData = [],
            RideDate = DateTimeOffset.UtcNow
        });

        await databaseContext.SaveChangesAsync(cancellationToken);

        // TODO: Save data to transactional outbox and have a separate process that reads from the outbox and saves to the file store.

        return await fileStore.StoreAsync(entity.Entity.Id, command.Content, command.ContentType, cancellationToken);
    }
}
