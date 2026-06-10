namespace Cycling.Rider.Tracking.Domain.Rides;

public sealed class Ride
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);
    public required DateTimeOffset RideDate { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
