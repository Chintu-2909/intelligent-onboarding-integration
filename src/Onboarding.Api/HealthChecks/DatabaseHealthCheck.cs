using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Onboarding.Api.Data;

namespace Onboarding.Api.HealthChecks;

public sealed class DatabaseHealthCheck(
    IServiceScopeFactory serviceScopeFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope =
                serviceScopeFactory.CreateAsyncScope();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<OnboardingDbContext>();

            var canConnect =
                await dbContext.Database
                    .CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy(
                    "SQLite database is available.")
                : HealthCheckResult.Unhealthy(
                    "SQLite database is unavailable.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy(
                "SQLite database health check failed.");
        }
    }
}
