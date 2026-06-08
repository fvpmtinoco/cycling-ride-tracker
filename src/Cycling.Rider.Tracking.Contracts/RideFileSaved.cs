namespace Cycling.Rider.Tracking.Contracts;

public sealed record RideFileSaved(Guid RideId, DateTimeOffset SavedAt);
