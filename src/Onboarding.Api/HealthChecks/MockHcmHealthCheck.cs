using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Onboarding.Api.HealthChecks;

public sealed class MockHcmHealthCheck(
    IHttpClientFactory httpClientFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            timeoutSource.CancelAfter(
                TimeSpan.FromSeconds(3));

            var httpClient =
                httpClientFactory.CreateClient(
                    "MockHcmApi");

            using var response =
                await httpClient.GetAsync(
                    "/api/employees",
                    timeoutSource.Token);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(
                    "Mock HCM API is available.")
                : HealthCheckResult.Unhealthy(
                    "Mock HCM API returned an unhealthy response.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy(
                "Mock HCM API is unavailable.");
        }
    }
}
