using Serilog.Context;

namespace MockHcm.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            GetOrCreateCorrelationId(context);

        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] =
                correlationId;

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(
                   "CorrelationId",
                   correlationId))
        {
            await next(context);
        }
    }

    private static string GetOrCreateCorrelationId(
        HttpContext context)
    {
        var incomingCorrelationId =
            context.Request.Headers[HeaderName]
                .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(
                incomingCorrelationId))
        {
            return incomingCorrelationId.Trim();
        }

        return Guid.NewGuid().ToString("N");
    }
}