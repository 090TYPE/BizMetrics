namespace BizMetrics.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Endpoint the API uses to reach object storage (e.g. http://minio:9000).</summary>
    public string Endpoint { get; set; } = "http://localhost:9000";

    /// <summary>
    /// Endpoint baked into presigned URLs handed to the browser. Differs from
    /// <see cref="Endpoint"/> under Docker, where the API talks to "minio" but the
    /// browser must use "localhost".
    /// </summary>
    public string PublicEndpoint { get; set; } = "http://localhost:9000";

    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Bucket { get; set; } = "bizmetrics";
}
