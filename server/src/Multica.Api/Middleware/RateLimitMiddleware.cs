using System.Net;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Multica.Api.Middleware;

/// <summary>
/// Per-IP rate limiting middleware backed by Redis.
/// Compatible with Go's RateLimit middleware implementation.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase? _redis;
    private readonly int _limit;
    private readonly int _windowSeconds;
    private readonly ILogger<RateLimitMiddleware> _logger;

    // Lua script: atomically increment counter and set TTL on first access
    private const string RateLimitScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return count
        """;

    public RateLimitMiddleware(
        RequestDelegate next,
        IDatabase? redis,
        IConfiguration config,
        ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _redis = redis;
        _limit = int.Parse(config["RateLimit:Limit"] ?? "100");
        _windowSeconds = int.Parse(config["RateLimit:WindowSeconds"] ?? "60");
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Fail-open if Redis is not available
        if (_redis is null)
        {
            await _next(context);
            return;
        }

        var ip = ExtractIP(context.Request);
        var key = RateLimitKey(context.Request.Path, ip);

        try
        {
            var result = await _redis.ScriptEvaluateAsync(
                RateLimitScript,
                new RedisKey[] { key },
                new RedisValue[] { _windowSeconds });

            var count = (long)result;
            if (count > _limit)
            {
                context.Response.Headers["Retry-After"] = _windowSeconds.ToString();
                context.Response.StatusCode = 429;
                await context.Response.WriteAsJsonAsync(new { error = "too many requests" });
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ratelimit: redis error; allowing request, ip={IP}", ip);
            // Fail-open: allow request on Redis error
        }

        await _next(context);
    }

    private static string ExtractIP(HttpRequest request)
    {
        var remoteHost = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

        // Check for X-Forwarded-For from trusted proxies
        // For now, we don't implement trusted proxy logic (matching Go's default behavior)
        // TODO: Implement trusted proxy CIDR matching if needed

        return remoteHost;
    }

    private static string RateLimitKey(string path, string ip)
    {
        var sanitized = path.TrimStart('/').Replace('/', ':');
        return $"mul:ratelimit:{sanitized}:{ip}";
    }
}

/// <summary>
/// Extension methods for registering rate limiting middleware.
/// </summary>
public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimit(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitMiddleware>();
    }
}
