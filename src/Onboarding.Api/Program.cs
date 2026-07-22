using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Onboarding.Api.Data;
using Onboarding.Api.ExceptionHandling;
using Onboarding.Api.Handlers;
using Onboarding.Api.Middleware;
using Onboarding.Api.Services;
using Serilog;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Onboarding.Api.HealthChecks;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(
                context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty(
                "Application",
                "Onboarding.Api");
    });

var allowedCorsOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? [];

if (allowedCorsOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "At least one CORS origin must be configured.");
}

builder.Services.Configure<FormOptions>(
    options =>
    {
        options.MultipartBodyLengthLimit =
            65_536;
    });

builder.WebHost.ConfigureKestrel(
    options =>
    {
        options.Limits.MaxRequestBodySize =
            65_536;
    });
    
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        "sqlite",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"])
    .AddCheck<MockHcmHealthCheck>(
        "mock-hcm",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"])
    .AddCheck<OllamaHealthCheck>(
        "ollama",
        failureStatus: HealthStatus.Degraded,
        tags: ["ready"]);
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "DashboardPolicy",
        policy =>
        {
            policy
                .WithOrigins(allowedCorsOrigins)
                .WithMethods(
                    HttpMethods.Get,
                    HttpMethods.Post,
                    HttpMethods.Options)
                .WithHeaders(
                    "Content-Type",
                    "X-Correlation-ID")
                .WithExposedHeaders(
                    "X-Correlation-ID",
                    "Retry-After")
                .SetPreflightMaxAge(
                    TimeSpan.FromMinutes(10));
        });
});

builder.Services.AddExceptionHandler<
    GlobalExceptionHandler>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected =
        async (context, cancellationToken) =>
        {
            var retryAfterSeconds = 60;

            if (context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
            {
                retryAfterSeconds =
                    Math.Max(
                        1,
                        (int)Math.Ceiling(
                            retryAfter.TotalSeconds));
            }

            context.HttpContext.Response.Headers.RetryAfter =
                retryAfterSeconds.ToString(
                    CultureInfo.InvariantCulture);

            var problemDetails =
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status429TooManyRequests,

                    Title =
                        "Too many requests.",

                    Detail =
                        "The request limit was exceeded. Retry after the period specified in the Retry-After header.",

                    Instance =
                        context.HttpContext.Request.Path
                };

            problemDetails.Extensions["correlationId"] =
                context.HttpContext.TraceIdentifier;

            await context.HttpContext.Response
                .WriteAsJsonAsync(
                    problemDetails,
                    cancellationToken);
        };

    options.AddPolicy(
        "ProcessingPolicy",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetClientPartitionKey(httpContext),
                _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,

                        Window =
                            TimeSpan.FromMinutes(1),

                        QueueLimit = 0,

                        AutoReplenishment = true
                    }));

    options.AddPolicy(
        "AiPolicy",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetClientPartitionKey(httpContext),
                _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,

                        Window =
                            TimeSpan.FromMinutes(1),

                        QueueLimit = 0,

                        AutoReplenishment = true
                    }));
});

builder.Services.AddSingleton<
    IErrorGuidanceCatalogue,
    ErrorGuidanceCatalogue>();

builder.Services.AddScoped<
    IAiFailureExplanationService,
    AiFailureExplanationService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<
    CorrelationIdDelegatingHandler>();

var connectionString =
    builder.Configuration.GetConnectionString(
        "OnboardingDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'OnboardingDatabase' was not found.");

builder.Services.AddDbContext<OnboardingDbContext>(
    options =>
        options.UseSqlite(connectionString));

var mockHcmBaseUrl =
    builder.Configuration["MockHcmApi:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Mock HCM API base URL was not configured.");

builder.Services
    .AddHttpClient(
        "MockHcmApi",
        client =>
        {
            client.BaseAddress =
                new Uri(mockHcmBaseUrl);

            client.Timeout =
                TimeSpan.FromSeconds(5);

            client.DefaultRequestHeaders.Add(
                "Accept",
                "application/json");
        })
    .AddHttpMessageHandler<
        CorrelationIdDelegatingHandler>();

var ollamaBaseUrl =
    builder.Configuration["Ollama:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Ollama base URL was not configured.");

var ollamaTimeoutSeconds =
    builder.Configuration.GetValue<int>(
        "Ollama:TimeoutSeconds");

builder.Services.AddHttpClient(
    "OllamaApi",
    client =>
    {
        client.BaseAddress =
            new Uri(ollamaBaseUrl);

        client.Timeout =
            TimeSpan.FromSeconds(
                ollamaTimeoutSeconds > 0
                    ? ollamaTimeoutSeconds
                    : 60);

        client.DefaultRequestHeaders.Add(
            "Accept",
            "application/json");
    });

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded " +
        "{StatusCode} in {Elapsed:0.0000} ms";

    options.EnrichDiagnosticContext =
        (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set(
                "CorrelationId",
                httpContext.TraceIdentifier);

            diagnosticContext.Set(
                "RequestHost",
                httpContext.Request.Host.Value);
        };
});

app.UseCors("DashboardPolicy");

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks(
        "/health/live",
        new HealthCheckOptions
        {
            Predicate =
                _ => false,

            ResponseWriter =
                WriteHealthResponseAsync
        })
    .DisableRateLimiting();

app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions
        {
            Predicate =
                registration =>
                    registration.Tags.Contains(
                        "ready"),

            ResponseWriter =
                WriteHealthResponseAsync
        })
    .DisableRateLimiting();

app.MapControllers();

try
{
    Log.Information(
        "Starting {ApplicationName}",
        "Onboarding.Api");

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(
        exception,
        "{ApplicationName} terminated unexpectedly",
        "Onboarding.Api");
}
finally
{
    Log.CloseAndFlush();
}

static string GetClientPartitionKey(
    HttpContext httpContext)
{
    return httpContext.Connection
        .RemoteIpAddress?
        .ToString()
        ?? "unknown-client";
}

static Task WriteHealthResponseAsync(
    HttpContext httpContext,
    HealthReport healthReport)
{
    httpContext.Response.ContentType =
        "application/json";

    var response =
        new
        { 
            status =
                healthReport.Status.ToString(),

            totalDurationMilliseconds =
                Math.Round(
                    healthReport.TotalDuration.TotalMilliseconds,
                    2),

            checks =
                healthReport.Entries.Select(
                    entry =>
                        new
                        {
                            name =
                                entry.Key,

                            status =
                                entry.Value.Status.ToString(),

                            description =
                                entry.Value.Description,

                            durationMilliseconds =
                                Math.Round(
                                    entry.Value.Duration
                                        .TotalMilliseconds,
                                    2)
                        }),

            correlationId =
                httpContext.TraceIdentifier
        };

    return httpContext.Response.WriteAsync(
        JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                WriteIndented =
                    true
            }));
}