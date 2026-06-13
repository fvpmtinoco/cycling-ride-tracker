using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Api.Filters;
using Cycling.Rider.Tracking.Api.Infrastructure;
using Cycling.Rider.Tracking.Application;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Cycling.Rider.Tracking.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddPresentation();
builder.Services.AddAuthenticationInternalServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidators();

builder.Services.AddScoped<ICommandHandler<SaveFileCommand, SaveFileResult>, SaveFileCommandHandler>();
builder.Services.AddScoped<IQueryHandler<ListFilesQuery, ListFilesResult>, ListFilesQueryHandler>();
builder.Services.AddScoped<IdempotencyFilter>();

// Minimal API: IEndpointFilter resolved from DI via .AddEndpointFilter<T>(), so it must be registered.
// MVC controllers use IFilterFactory (the [Idempotent] attribute) instead — no separate registration needed.
builder.Services.AddScoped<IdempotencyEndpointFilter>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

//builder.Services.AddLogging(c =>
//{
//    c.AddConsole();
//}); 

builder.Logging.AddConsole();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.ApplyMigrationsAtStartup();
    app.UseSwaggerWithUi();

}

app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Minimal API equivalent of POST /files/fit (MVC controller).
// Differences from the MVC version:
//   - Idempotency: [Idempotent] attribute (IFilterFactory) does not apply to Minimal API endpoints.
//     Use .AddEndpointFilter<IdempotencyEndpointFilter>() instead.
//   - Authorization: [Authorize] on the controller is replaced by .RequireAuthorization().
//   - Handler dependencies are injected as parameters, not via constructor DI.
app.MapPost("files/minimal", async (
        IFormFile fit,
        [FromQuery] DateTimeOffset rideDate,
        [FromServices] ICommandHandler<SaveFileCommand, SaveFileResult> handler,
        CancellationToken cancellationToken) =>
    {
        var result = await handler.HandleAsync(
            new SaveFileCommand(fit.OpenReadStream(), fit.ContentType, rideDate),
            cancellationToken);

        return result.Match(
            value => Results.Created($"/files/{value.Id}", value),
            CustomResults.Problem);
    })
    .RequireAuthorization()
    .AddEndpointFilter<IdempotencyEndpointFilter>();

await app.RunAsync();

//public partial class Program;
