using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Auth;
using Multica.Core.Entities;
using Multica.Infrastructure.Auth;
using Multica.Infrastructure.Data;

namespace Multica.Api.Middleware;

/// <summary>
/// Authentication middleware with multi-token-prefix routing.
/// Compatible with Go's Auth middleware implementation.
/// </summary>
public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthMiddleware> _logger;

    public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        JwtTokenService jwtService,
        PatCache patCache,
        DaemonTokenCache daemonTokenCache,
        TaskTokenValidator taskTokenValidator,
        CloudPatVerifier? cloudPatVerifier,
        MulticaDbContext db)
    {
        // Strip any client-supplied X-Actor-Source header
        context.Request.Headers.Remove("X-Actor-Source");

        // Extract token
        var (token, fromCookie) = ExtractToken(context.Request);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogDebug("auth: no token found, path={Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "missing authorization" });
            return;
        }

        // Cookie-based auth requires CSRF validation for state-changing methods
        if (fromCookie && !CsrfValidator.Validate(context.Request))
        {
            _logger.LogDebug("auth: CSRF validation failed, path={Path}", context.Request.Path);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "CSRF validation failed" });
            return;
        }

        // Agent task token: "mat_" prefix
        if (token.StartsWith("mat_"))
        {
            var hash = TokenHasher.HashToken(token);
            var identity = await taskTokenValidator.ValidateAsync(hash);
            if (identity is null)
            {
                _logger.LogWarning("auth: invalid task token, path={Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "invalid token" });
                return;
            }

            context.Request.Headers["X-User-ID"] = identity.UserId.ToString();
            context.Request.Headers["X-Agent-ID"] = identity.AgentId.ToString();
            context.Request.Headers["X-Task-ID"] = identity.TaskId.ToString();
            context.Request.Headers["X-Workspace-ID"] = identity.WorkspaceId.ToString();
            context.Request.Headers["X-Actor-Source"] = "task_token";

            await _next(context);
            return;
        }

        // Cloud Node PAT: "mcn_" prefix
        if (token.StartsWith("mcn_"))
        {
            if (cloudPatVerifier is null)
            {
                _logger.LogWarning("auth: mcn_ token presented but cloud verifier not configured, path={Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "invalid token" });
                return;
            }

            try
            {
                var identity = await cloudPatVerifier.VerifyAsync(token, async (ownerId) =>
                {
                    var exists = await db.Users.AnyAsync(u => u.Id == Guid.Parse(ownerId));
                    return exists;
                });

                context.Request.Headers["X-User-ID"] = identity.OwnerId;
                await _next(context);
                return;
            }
            catch (CloudPatInvalidException ex)
            {
                _logger.LogWarning("auth: cloud rejected mcn_ token, path={Path}, error={Error}", context.Request.Path, ex.Message);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "invalid token" });
                return;
            }
            catch (CloudPatUnavailableException ex)
            {
                _logger.LogWarning("auth: cloud pat verify unavailable, path={Path}, error={Error}", context.Request.Path, ex.Message);
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { error = "cloud pat verifier unavailable" });
                return;
            }
        }

        // PAT: tokens starting with "mul_"
        if (token.StartsWith("mul_"))
        {
            var hash = TokenHasher.HashToken(token);

            // Check cache first
            var cachedUserId = await patCache.GetAsync(hash);
            if (cachedUserId is not null)
            {
                context.Request.Headers["X-User-ID"] = cachedUserId;
                await _next(context);
                return;
            }

            // Cache miss: query database
            var pat = await db.PersonalAccessTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (pat is null)
            {
                _logger.LogWarning("auth: invalid PAT, path={Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "invalid token" });
                return;
            }

            var userId = pat.UserId.ToString();
            context.Request.Headers["X-User-ID"] = userId;

            // Cache with TTL clamped to token's remaining lifetime
            var ttl = PatCache.TTLForExpiry(DateTime.UtcNow, pat.ExpiresAt?.DateTime);
            await patCache.SetAsync(hash, userId, ttl);

            await _next(context);
            return;
        }

        // JWT (default path)
        var principal = jwtService.ValidateToken(token);
        if (principal is null)
        {
            _logger.LogWarning("auth: invalid JWT, path={Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid token" });
            return;
        }

        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub))
        {
            _logger.LogWarning("auth: invalid JWT claims, path={Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid claims" });
            return;
        }

        context.Request.Headers["X-User-ID"] = sub;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (!string.IsNullOrEmpty(email))
        {
            context.Request.Headers["X-User-Email"] = email;
        }

        await _next(context);
    }

    private static (string token, bool fromCookie) ExtractToken(HttpRequest request)
    {
        // Priority: Authorization header > multica_auth cookie
        var authHeader = request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer "))
        {
            return (authHeader[7..], false);
        }

        if (request.Cookies.TryGetValue(CookieService.AuthCookieName, out var cookie) && !string.IsNullOrEmpty(cookie))
        {
            return (cookie, true);
        }

        return ("", false);
    }
}
