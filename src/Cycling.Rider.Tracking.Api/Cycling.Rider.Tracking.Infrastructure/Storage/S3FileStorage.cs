using Amazon.S3;
using Amazon.S3.Model;
using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Infrastructure.Options;
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
                ContentType = contentType
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
