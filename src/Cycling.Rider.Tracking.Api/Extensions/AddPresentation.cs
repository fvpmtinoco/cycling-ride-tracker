using Cycling.Rider.Tracking.Api.Filters;

namespace Cycling.Rider.Tracking.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddSwaggerGen(c => c.OperationFilter<IdempotencyHeaderOperationFilter>());

        return services;
    }
}
