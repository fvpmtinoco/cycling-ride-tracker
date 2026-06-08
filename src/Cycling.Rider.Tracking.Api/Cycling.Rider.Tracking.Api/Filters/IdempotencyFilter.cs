using System.Security.Cryptography;
using System.Text;
using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Domain.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IdempotencyFilter>();
}

public sealed class IdempotencyFilter(
    IDatabaseContext databaseContext,
    ILogger<IdempotencyFilter> logger) : IAsyncResourceFilter
{
    private const string HeaderName = "Idempotency-Key";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues))
        {
            await next();
            return;
        }

        string key = keyValues.ToString();
        string requestHash = await ComputeRequestHashAsync(context.HttpContext.Request, context.HttpContext.RequestAborted);

        var existing = await databaseContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                k => k.Key == key && k.ExpiresAt > DateTimeOffset.UtcNow,
                context.HttpContext.RequestAborted);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "IdempotencyKey.Conflict",
                    Detail = "The idempotency key was already used with a different request body."
                })
                { StatusCode = StatusCodes.Status422UnprocessableEntity };
                return;
            }

            context.Result = new ContentResult
            {
                Content = existing.ResponseBody,
                ContentType = "application/json",
                StatusCode = existing.ResponseStatusCode
            };
            return;
        }

        var originalBody = context.HttpContext.Response.Body;
        using var buffer = new MemoryStream();
        context.HttpContext.Response.Body = buffer;

        try
        {
            await next();

            int statusCode = context.HttpContext.Response.StatusCode;
            buffer.Position = 0;
            string responseBody;
            using (var reader = new StreamReader(buffer, leaveOpen: true))
            {
                responseBody = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.HttpContext.RequestAborted);

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
                }, context.HttpContext.RequestAborted);

                await databaseContext.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
            catch (DbUpdateException ex)
            {
                logger.LogDebug(ex, "Idempotency key {Key} already stored by a concurrent request.", key);
            }
        }
        finally
        {
            context.HttpContext.Response.Body = originalBody;
        }
    }

    // Hash semantic content (query string + sorted form fields + file bytes) not raw multipart
    // body bytes, because MultipartFormDataContent generates a new random boundary per instance.
    private static async Task<string> ComputeRequestHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);

        using var ms = new MemoryStream();

        await ms.WriteAsync(Encoding.UTF8.GetBytes(request.QueryString.Value ?? string.Empty), cancellationToken);

        foreach (var fieldKey in form.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            await ms.WriteAsync(Encoding.UTF8.GetBytes($"{fieldKey}={form[fieldKey]}"), cancellationToken);
        }

        foreach (var file in form.Files.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            using var fileStream = file.OpenReadStream();
            await fileStream.CopyToAsync(ms, cancellationToken);
        }

        return Convert.ToHexString(SHA256.HashData(ms.ToArray()));
    }
}
