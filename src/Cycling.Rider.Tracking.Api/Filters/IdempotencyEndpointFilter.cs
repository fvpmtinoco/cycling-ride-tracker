using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Api.Filters;

public sealed class IdempotencyEndpointFilter(
    IDatabaseContext databaseContext,
    ILogger<IdempotencyEndpointFilter> logger) : IEndpointFilter
{
    private const string HeaderName = "Idempotency-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues))
        {
            return await next(context);
        }

        string key = keyValues.ToString();
        string requestHash = await IdempotencyHasher.ComputeAsync(httpContext.Request, httpContext.RequestAborted);

        var existing = await databaseContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                k => k.Key == key && k.ExpiresAt > DateTimeOffset.UtcNow,
                httpContext.RequestAborted);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                return Results.Problem(
                    title: "IdempotencyKey.Conflict",
                    detail: "The idempotency key was already used with a different request body.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            return new ReplayResult(existing.ResponseBody, existing.ResponseStatusCode);
        }

        // In IEndpointFilter, next() returns the handler's IResult without executing it yet —
        // the framework calls ExecuteAsync on whatever this method returns. Wrapping lets us
        // capture the response body and status code during that execution.
        var handlerResult = await next(context);

        return handlerResult is IResult result
            ? new CapturingResult(result, key, requestHash, databaseContext, logger)
            : handlerResult;
    }
}

file sealed class ReplayResult(string body, int statusCode) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync(body, httpContext.RequestAborted);
    }
}

file sealed class CapturingResult(
    IResult inner,
    string key,
    string requestHash,
    IDatabaseContext databaseContext,
    ILogger logger) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var originalBody = httpContext.Response.Body;
        using var buffer = new MemoryStream();
        httpContext.Response.Body = buffer;

        try
        {
            await inner.ExecuteAsync(httpContext);

            int statusCode = httpContext.Response.StatusCode;
            buffer.Position = 0;
            string responseBody;
            using (var reader = new StreamReader(buffer, leaveOpen: true))
            {
                responseBody = await reader.ReadToEndAsync(httpContext.RequestAborted);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, httpContext.RequestAborted);

            try
            {
                await databaseContext.IdempotencyKeys.AddAsync(new IdempotencyKey
                {
                    Key = key,
                    RequestHash = requestHash,
                    ResponseStatusCode = statusCode,
                    ResponseBody = responseBody,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
                }, httpContext.RequestAborted);

                await databaseContext.SaveChangesAsync(httpContext.RequestAborted);
            }
            catch (DbUpdateException ex)
            {
                logger.LogDebug(ex, "Idempotency key {Key} already stored by a concurrent request.", key);
            }
        }
        finally
        {
            httpContext.Response.Body = originalBody;
        }
    }
}
