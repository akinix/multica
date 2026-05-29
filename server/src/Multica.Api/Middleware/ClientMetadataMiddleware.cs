using System.Diagnostics;

namespace Multica.Api.Middleware;

/// <summary>
/// Middleware that extracts client metadata headers and stores them in context.
/// Compatible with Go's ClientMetadata middleware.
/// </summary>
public class ClientMetadataMiddleware
{
    private readonly RequestDelegate _next;

    public const string HeaderClientPlatform = "X-Client-Platform";
    public const string HeaderClientVersion = "X-Client-Version";
    public const string HeaderClientOS = "X-Client-OS";

    public ClientMetadataMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var platform = context.Request.Headers[HeaderClientPlatform].ToString();
        var version = context.Request.Headers[HeaderClientVersion].ToString();
        var os = context.Request.Headers[HeaderClientOS].ToString();

        // Store in context
        context.Items["ClientPlatform"] = platform;
        context.Items["ClientVersion"] = version;
        context.Items["ClientOS"] = os;

        // Set on Activity for distributed tracing
        var activity = Activity.Current;
        if (activity is not null)
        {
            if (!string.IsNullOrEmpty(platform))
                activity.SetTag("client.platform", platform);
            if (!string.IsNullOrEmpty(version))
                activity.SetTag("client.version", version);
            if (!string.IsNullOrEmpty(os))
                activity.SetTag("client.os", os);
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for accessing client metadata from HttpContext.
/// </summary>
public static class ClientMetadataExtensions
{
    public static (string platform, string version, string os) GetClientMetadata(this HttpContext context)
    {
        return (
            context.Items["ClientPlatform"]?.ToString() ?? "",
            context.Items["ClientVersion"]?.ToString() ?? "",
            context.Items["ClientOS"]?.ToString() ?? ""
        );
    }
}
