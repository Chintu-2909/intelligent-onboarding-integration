using Onboarding.Api.Middleware;

namespace Onboarding.Api.Handlers;

public sealed class CorrelationIdDelegatingHandler(
    IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext =
            httpContextAccessor.HttpContext;

        var correlationId =
            httpContext?.TraceIdentifier;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.Remove(
                CorrelationIdMiddleware.HeaderName);

            request.Headers.TryAddWithoutValidation(
                CorrelationIdMiddleware.HeaderName,
                correlationId);
        }

        return await base.SendAsync(
            request,
            cancellationToken);
    }
}