using Cycling.Rider.Tracking.Application.Files;
using FluentValidation;

namespace Cycling.Rider.Tracking.Application.Validators;

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
