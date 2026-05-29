using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Multica.Api.HealthChecks;

public class CustomHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Multica API is running"));
    }
}
