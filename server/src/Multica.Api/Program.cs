using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Multica.Api.Handlers;
using Multica.Api.Middleware;
using Multica.Core.Auth;
using Multica.Infrastructure;
using Multica.Infrastructure.Data;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Multica")
    .WriteTo.Console(new Serilog.Formatting.Compact.RenderedCompactJsonFormatter()));

// Infrastructure (DbContext, Redis, Health Checks)
builder.Services.AddInfrastructure(builder.Configuration);

// Auth services
builder.Services.AddSingleton<CookieService>();
builder.Services.AddSingleton<GoogleOAuthService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<SignupControlService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:3000"])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware pipeline (order matters)
// 1. Request ID
app.UseMiddleware<RequestIdMiddleware>();

// 2. Client Metadata
app.UseMiddleware<ClientMetadataMiddleware>();

// 3. Request Logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "");
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString() ?? "");
        diagnosticContext.Set("ClientPlatform", httpContext.Request.Headers["X-Client-Platform"].ToString() ?? "");
        diagnosticContext.Set("ClientVersion", httpContext.Request.Headers["X-Client-Version"].ToString() ?? "");
        diagnosticContext.Set("ClientOS", httpContext.Request.Headers["X-Client-OS"].ToString() ?? "");
        diagnosticContext.Set("RequestId", httpContext.Items["RequestId"]?.ToString() ?? "");
        diagnosticContext.Set("UserId", httpContext.Request.Headers["X-User-ID"].ToString() ?? "");

        // Webhook path redaction
        var path = httpContext.Request.Path.Value ?? "";
        if (path.StartsWith("/api/webhooks/autopilots/"))
        {
            var segments = path.Split('/');
            if (segments.Length > 4)
            {
                segments[4] = "[redacted]";
                diagnosticContext.Set("RedactedPath", string.Join("/", segments));
            }
        }

        // Error enrichment
        if (httpContext.Response.StatusCode >= 500)
        {
            diagnosticContext.Set("Error", true);
        }
    };

    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        // Slow request detection (elapsed is in seconds)
        if (elapsed > 5)
            return Serilog.Events.LogEventLevel.Error;
        if (elapsed > 1)
            return Serilog.Events.LogEventLevel.Warning;

        // Health endpoint skip
        if (httpContext.Request.Path.StartsWithSegments("/health"))
            return Serilog.Events.LogEventLevel.Verbose;

        return Serilog.Events.LogEventLevel.Information;
    };
});

// 4. Exception Handler
app.UseExceptionHandler(error =>
{
    error.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            title = "Internal Server Error",
            status = 500,
            detail = app.Environment.IsDevelopment() ? exception?.Message : null
        });
    });
});

// 5. CSP
app.UseMiddleware<CspMiddleware>();

// 6. CORS
app.UseCors();

// 7. Rate Limiting
app.UseRateLimit();

// 8. HTTP Metrics (prometheus-net)
app.UseHttpMetrics();

// Health checks
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Auth endpoints
app.MapAuthEndpoints();

// Metrics endpoint
app.MapMetrics();

app.MapGet("/", () => "Multica API");

app.Run();

public partial class Program { }
