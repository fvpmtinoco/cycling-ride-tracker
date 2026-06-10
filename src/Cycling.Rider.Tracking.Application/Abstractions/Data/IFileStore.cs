namespace Cycling.Rider.Tracking.Application.Abstractions.Data;

public record FileLocation(Guid rideId, string Bucket, string Key, long SizeBytes);

public interface IFileStore
{
    Task<FileLocation> StoreAsync(Guid rideId, Stream content, string contentType, CancellationToken cancellationToken);
}
