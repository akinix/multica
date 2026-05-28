namespace Multica.Api.Middleware;

/// <summary>
/// Content-Security-Policy header middleware.
/// Compatible with Go's CSP middleware.
/// </summary>
public class CspMiddleware
{
    private readonly RequestDelegate _next;

    private const string CspHeader = "Content-Security-Policy";
    private const string CspValue = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' https: data:; connect-src 'self' wss:; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'";

    public CspMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers[CspHeader] = CspValue;
        await _next(context);
    }
}
