using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Abstractions.Validation;
using Cycling.Rider.Tracking.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cycling.Rider.Tracking.Application.Files;

public sealed record SaveFileCommand(Stream Content, string ContentType, DateTimeOffset RideDate) : ICommand<SaveFileResult>;
public sealed record SaveFileResult(Guid Id);

public sealed class SaveFileCommandValidator : AbstractValidator<SaveFileCommand>
{
    public SaveFileCommandValidator()
    {
        RuleFor(command => command.RideDate)
            .GreaterThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMonths(-6))
            .WithErrorCode("RideDate.TooOld")
            .WithMessage("Ride date cannot be older than 6 months.");

        RuleFor(command => command.RideDate)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow)
            .WithErrorCode("RideDate.InFuture")
            .WithMessage("Ride date cannot be in the future.");
    }
}

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

                // TODO: Save data to transactional outbox and have a separate process that reads from the outbox and saves to the file store.
                logger.LogInformation("Generated file with Id: {Id}", ride.Entity.Id);

                return Result.Success(new SaveFileResult(ride.Entity.Id));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                // TODO:  Log the exception here.

                return Result.Failure<SaveFileResult>(Error.Failure("save_file_failed", "Failed to save the file."));
            }
        }
    }
}
