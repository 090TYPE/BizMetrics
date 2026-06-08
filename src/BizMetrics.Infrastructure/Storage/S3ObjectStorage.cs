using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace BizMetrics.Infrastructure.Storage;

/// <summary>
/// S3-compatible object storage (MinIO in dev). Uses path-style addressing, which
/// MinIO requires. A second client bound to the public endpoint generates presigned
/// URLs the browser can reach.
/// </summary>
public class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _publicClient;
    private readonly StorageOptions _opt;

    public S3ObjectStorage(IOptions<StorageOptions> options)
    {
        _opt = options.Value;
        _client = BuildClient(_opt.Endpoint);
        _publicClient = BuildClient(_opt.PublicEndpoint);
    }

    private AmazonS3Client BuildClient(string serviceUrl) => new(
        _opt.AccessKey, _opt.SecretKey,
        new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        });

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_client, _opt.Bucket);
        if (!exists)
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _opt.Bucket }, ct);
    }

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) =>
        _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opt.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);

    public async Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(_opt.Bucket, key, ct);
        return response.ResponseStream;
    }

    public string GetPresignedDownloadUrl(string key, TimeSpan expiry)
    {
        var url = _publicClient.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _opt.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry)
        });

        // The SDK defaults to https; honor the configured public endpoint's scheme
        // so http-only MinIO works in dev. The scheme isn't part of the signature.
        var wantHttp = _opt.PublicEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        if (wantHttp && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "http://" + url["https://".Length..];
        return url;
    }

    public void Dispose()
    {
        _client.Dispose();
        _publicClient.Dispose();
    }
}
