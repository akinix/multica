namespace Multica.Api.Middleware;

/// <summary>
/// Middleware that generates and sets a unique request ID for each request.
/// Compatible with Go's RequestID middleware.
/// </summary>
public class RequestIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string RequestIdHeader = "X-Request-Id";

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Use existing X-Request-Id from trusted proxy, or generate new one
        var requestId = context.Request.Headers[RequestIdHeader].ToString();
        if (string.IsNullOrEmpty(requestId))
        {
            requestId = Guid.NewGuid().ToString();
        }

        // Store in context for downstream use
        context.Items["RequestId"] = requestId;

        // Set on response
        context.Response.Headers[RequestIdHeader] = requestId;

        await _next(context);
    }
}
