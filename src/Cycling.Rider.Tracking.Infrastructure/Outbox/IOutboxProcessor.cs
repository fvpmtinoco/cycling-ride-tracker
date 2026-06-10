namespace Cycling.Rider.Tracking.Infrastructure.Outbox;

public interface IOutboxProcessor
{
    Task ProcessOutboxAsync(CancellationToken cancellationToken);
}
