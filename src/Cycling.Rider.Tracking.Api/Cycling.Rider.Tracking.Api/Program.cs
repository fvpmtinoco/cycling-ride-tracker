using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Cycling.Rider.Tracking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddPresentation();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICommandHandler<SaveFileCommand, FileLocation>, SaveFileCommandHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.ApplyMigrationsAtStartup();
    app.UseSwaggerWithUi();

}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
