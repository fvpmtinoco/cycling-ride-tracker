using Cycling.Rider.Tracking.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Cycling.Rider.Tracking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
        => services.AddValidatorsFromAssemblyContaining<SaveFileCommandValidator>(ServiceLifetime.Scoped);
}
