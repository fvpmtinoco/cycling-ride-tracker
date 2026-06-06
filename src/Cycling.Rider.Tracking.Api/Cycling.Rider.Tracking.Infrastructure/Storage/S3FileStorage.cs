using Amazon.S3;
using Amazon.S3.Model;
using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Microsoft.Extensions.Options;

namespace Cycling.Rider.Tracking.Infrastructure.Storage;

public class S3FileStorage(IAmazonS3 s3, IOptions<S3StorageOptions> options) : IFileStore
{
    public async Task<FileLocation> StoreAsync(Guid rideId, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var length = content.Length;
        try
        {
            var uploadRequest = new PutObjectRequest
            {
                BucketName = options.Value.Bucket,
                Key = rideId.ToString(),
                InputStream = content,
                ContentType = "text/plain"
            };

            await s3.PutObjectAsync(uploadRequest, cancellationToken);
        }
        finally
        {
            await content.DisposeAsync();
        }

        return new FileLocation(rideId, options.Value.Bucket, rideId.ToString(), length);
    }
}

public record S3StorageOptions
{
    public required string ServiceUrl { get; init; }
    public required string Bucket { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
}
