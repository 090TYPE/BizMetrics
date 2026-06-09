using BizMetrics.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BizMetrics.Api.Health;

public class StorageHealthCheck : IHealthCheck
{
    private readonly IObjectStorage _storage;

    public StorageHealthCheck(IObjectStorage storage) => _storage = storage;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            // EnsureBucketAsync is idempotent — safe to call as a ping.
            await _storage.EnsureBucketAsync(ct);
            return HealthCheckResult.Healthy("Object storage is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Object storage is unreachable.", ex);
        }
    }
}
