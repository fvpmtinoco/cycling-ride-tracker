using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Api.Filters;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Cycling.Rider.Tracking.Infrastructure;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddPresentation();
builder.Services.AddAuthenticationInternalServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICommandHandler<SaveFileCommand, SaveFileResult>, SaveFileCommandHandler>();
builder.Services.AddScoped<IValidator<SaveFileCommand>, SaveFileCommandValidator>();
builder.Services.AddScoped<IQueryHandler<ListFilesQuery, ListFilesResult>, ListFilesQueryHandler>();
builder.Services.AddScoped<IValidator<ListFilesQuery>, ListFilesQueryValidator>();
builder.Services.AddScoped<IdempotencyFilter>();

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

//app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

//public partial class Program;
