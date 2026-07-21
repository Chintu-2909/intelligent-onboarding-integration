using Microsoft.EntityFrameworkCore;
using Onboarding.Api.Data;
using Onboarding.Api.Middleware;
using Serilog;
using Onboarding.Api.Handlers;

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

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();

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