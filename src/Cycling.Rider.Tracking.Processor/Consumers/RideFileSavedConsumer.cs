using Cycling.Rider.Tracking.Contracts;
using MassTransit;

namespace Cycling.Rider.Tracking.Processor.Consumers;

public sealed class RideFileSavedConsumer(ILogger<RideFileSavedConsumer> logger) : IConsumer<RideFileSaved>
{
    public Task Consume(ConsumeContext<RideFileSaved> context)
    {
        logger.LogInformation(
            "Processing ride file: RideId={RideId}, SavedAt={SavedAt}",
            context.Message.RideId,
            context.Message.SavedAt);

        return Task.CompletedTask;
    }
}
