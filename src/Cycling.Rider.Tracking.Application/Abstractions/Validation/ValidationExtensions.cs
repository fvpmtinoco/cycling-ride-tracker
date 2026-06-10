using Cycling.Rider.Tracking.SharedKernel;
using FluentValidation.Results;

namespace Cycling.Rider.Tracking.Application.Abstractions.Validation;

public static class ValidationExtensions
{
    public static ValidationError ToValidationError(this ValidationResult result) =>
        new([.. result.Errors.Select(failure =>
            new Error(failure.ErrorCode ?? failure.PropertyName, failure.ErrorMessage, ErrorType.Validation))]);
}
