using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Abstractions.Validation;
using Cycling.Rider.Tracking.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cycling.Rider.Tracking.Application.Files;

public sealed record SaveFileCommand(Stream Content, string ContentType, DateTimeOffset RideDate) : ICommand<SaveFileResult>;
public sealed record SaveFileResult(Guid Id);

public sealed class SaveFileCommandHandler(
    IDatabaseContext databaseContext,
    IValidator<SaveFileCommand> validator,
    ILogger<SaveFileCommandHandler> logger) : ICommandHandler<SaveFileCommand, SaveFileResult>
{
    public async Task<Result<SaveFileResult>> HandleAsync(SaveFileCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SaveFileResult>(validation.ToValidationError());
        }

        // Simply could upload to S3 here. If the transaction below fails, orphaned file.
        // Don't track this with S3 tags (the tag write can fail too). Instead, a periodic
        // sweep deletes any S3 object older than N hours that has no referencing Ride row.

        using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["Request"] = "test"
        })
        )
        {
            try
            {
                var ride = await databaseContext.Rides.AddAsync(new Domain.Rides.Ride
                {
                    CreatedAt = DateTimeOffset.UtcNow,
                    RideDate = command.RideDate
                }, cancellationToken);

                using var ms = new MemoryStream();
                await command.Content.CopyToAsync(ms, cancellationToken);

                await databaseContext.TransactionFiles.AddAsync(new Domain.Outbox.TransactionFile
                {
                    FileContent = ms.ToArray(),
                    FileId = ride.Entity.Id,
                    ContentType = command.ContentType,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Processed = false
                }, cancellationToken);

                await databaseContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation("Generated file with Id: {Id}", ride.Entity.Id);

                return Result.Success(new SaveFileResult(ride.Entity.Id));
            }
            catch (Exception ex)
            {
                // Not necessary, transaction rolls back because of being scoped. For clarity purposes only
                await transaction.RollbackAsync(cancellationToken);

                logger.LogError(ex, "Error ocurred while saving the tracking file");
                return Result.Failure<SaveFileResult>(Error.Failure("save_file_failed", "Failed to save the file."));
            }
        }
    }
}
