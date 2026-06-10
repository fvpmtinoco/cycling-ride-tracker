using Cycling.Rider.Tracking.Processor.Consumers;
using MassTransit;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RideFileSavedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetSection("Messaging").GetValue<string>("Host")!);
        cfg.ConfigureEndpoints(ctx);
    });
});

await builder.Build().RunAsync();
