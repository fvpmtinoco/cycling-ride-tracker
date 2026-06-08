using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Cycling.Rider.Tracking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddPresentation();
builder.Services.AddAuthenticationInternalServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICommandHandler<SaveFileCommand, SaveFileResult>, SaveFileCommandHandler>();

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
