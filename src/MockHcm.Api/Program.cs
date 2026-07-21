using MockHcm.Api.Middleware;
using Serilog;

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
                "MockHcm.Api");
    });

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
        "MockHcm.Api");

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(
        exception,
        "{ApplicationName} terminated unexpectedly",
        "MockHcm.Api");
}
finally
{
    Log.CloseAndFlush();
}