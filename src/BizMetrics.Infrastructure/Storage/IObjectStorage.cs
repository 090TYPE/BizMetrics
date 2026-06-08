namespace BizMetrics.Infrastructure.Storage;

/// <summary>Object storage seam (backed by S3/MinIO). Keeps controllers and the worker provider-agnostic.</summary>
public interface IObjectStorage
{
    Task EnsureBucketAsync(CancellationToken ct = default);
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> GetAsync(string key, CancellationToken ct = default);

    /// <summary>A time-limited URL the browser can GET directly, using the public endpoint.</summary>
    string GetPresignedDownloadUrl(string key, TimeSpan expiry);
}
