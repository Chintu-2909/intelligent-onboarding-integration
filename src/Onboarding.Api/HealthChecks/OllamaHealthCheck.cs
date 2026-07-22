using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Onboarding.Api.HealthChecks;

public sealed class OllamaHealthCheck(
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
                    "OllamaApi");

            using var response =
                await httpClient.GetAsync(
                    "/api/tags",
                    timeoutSource.Token);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(
                    "Ollama API is available.")
                : HealthCheckResult.Degraded(
                    "Ollama API returned an unhealthy response.");
        }
        catch
        {
            return HealthCheckResult.Degraded(
                "Ollama API is unavailable. Approved deterministic guidance remains available.");
        }
    }
}
