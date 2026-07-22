using System.Text.RegularExpressions;
using Serilog.Context;

namespace Onboarding.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName =
        "X-Correlation-ID";

    private const int MaximumLength = 64;

    private static readonly Regex ValidCorrelationIdPattern =
        new(
            "^[A-Za-z0-9-]+$",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    public async Task InvokeAsync(
        HttpContext context)
    {
        var correlationId =
            GetOrCreateCorrelationId(context);

        context.TraceIdentifier =
            correlationId;

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

    private string GetOrCreateCorrelationId(
        HttpContext context)
    {
        var incomingCorrelationId =
            context.Request.Headers[HeaderName]
                .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(
                incomingCorrelationId))
        {
            return CreateCorrelationId();
        }

        var trimmedCorrelationId =
            incomingCorrelationId.Trim();

        if (IsValidCorrelationId(
                trimmedCorrelationId))
        {
            return trimmedCorrelationId;
        }

        logger.LogWarning(
            "Invalid incoming correlation ID was replaced with a generated value");

        return CreateCorrelationId();
    }

    private static bool IsValidCorrelationId(
        string correlationId)
    {
        return
            correlationId.Length <= MaximumLength &&
            ValidCorrelationIdPattern.IsMatch(
                correlationId);
    }

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid()
            .ToString("N");
    }
}