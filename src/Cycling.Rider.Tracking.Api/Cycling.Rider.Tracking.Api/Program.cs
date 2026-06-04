using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.ApplyMigrationsAtStartup();

}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
