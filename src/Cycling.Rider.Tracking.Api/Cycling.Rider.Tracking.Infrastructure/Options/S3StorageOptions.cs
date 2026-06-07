namespace Cycling.Rider.Tracking.Infrastructure.Options;

public record S3StorageOptions
{
    public required string ServiceUrl { get; init; }
    public required string Bucket { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
}
