using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cycling.Rider.Tracking.Api.Filters;

public class IdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        bool isIdempotent = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<IdempotentAttribute>()
            .Any();

        if (!isIdempotent)
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
            Description = "Optional. Same key replays the original response; reused with a different body returns 422."
        });
    }
}
