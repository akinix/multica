using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Auth;
using Multica.Infrastructure.Auth;
using Multica.Infrastructure.Data;

namespace Multica.Api.Middleware;

/// <summary>
/// Daemon authentication middleware for daemon-specific routes.
/// Only accepts Authorization header (no cookie fallback).
/// Compatible with Go's DaemonAuth middleware implementation.
/// </summary>
public class DaemonAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DaemonAuthMiddleware> _logger;

    public DaemonAuthMiddleware(RequestDelegate next, ILogger<DaemonAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        JwtTokenService jwtService,
        PatCache patCache,
        DaemonTokenCache daemonTokenCache,
        CloudPatVerifier? cloudPatVerifier,
        MulticaDbContext db)
    {
        // Only accept Authorization header (no cookie fallback for daemon)
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogDebug("daemon_auth: missing authorization header, path={Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "missing authorization header" });
            return;
        }

        var token = authHeader.StartsWith("Bearer ") ? authHeader[7..] : null;
        if (token is null)
        {
            _logger.LogDebug("daemon_auth: invalid format, path={Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid authorization format" });
            return;
        }

        // Daemon token: "mdt_" prefix
        if (token.StartsWith("mdt_"))
        {
            var hash = TokenHasher.HashToken(token);

            // Check cache first
            var cachedIdentity = await daemonTokenCache.GetAsync(hash);
            if (cachedIdentity is not null)
            {
                context.Items["DaemonWorkspaceId"] = cachedIdentity.WorkspaceId;
                context.Items["DaemonId"] = cachedIdentity.DaemonId;
                context.Items["DaemonAuthPath"] = "daemon_token";

                await _next(context);
                return;
            }

            // Cache miss: query database
            var dt = await db.DaemonTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (dt is null)
            {
                _logger.LogWarning("daemon_auth: invalid daemon token, path={Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "invalid daemon token" });
                return;
            }

            var identity = new DaemonTokenIdentity
            {
                WorkspaceId = dt.WorkspaceId.ToString(),
                DaemonId = dt.DaemonId
            };

            // Cache with TTL clamped to token's remaining lifetime
            var expiresAt = dt.ExpiresAt != default ? dt.ExpiresAt.DateTime : (DateTime?)null;
            var ttl = PatCache.TTLForExpiry(DateTime.UtcNow, expiresAt);
            await daemonTokenCache.SetAsync(hash, identity, ttl);

            context.Items["DaemonWorkspaceId"] = identity.WorkspaceId;
            context.Items["DaemonId"] = identity.DaemonId;
            context.Items["DaemonAuthPath"] = "daemon_token";

            await _next(context);
            return;
        }

        // Cloud Node PAT: "mcn_" prefix
        if (token.StartsWith("mcn_"))
        {
            if (cloudPatVerifier is null)
            {
                _logger.LogWarning("daemon_auth: mcn_ token presented but cloud verifier not configured, path={Path}", context.Request.Path);
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
                context.Items["DaemonAuthPath"] = "cloud_pat";

                await _next(context);
                return;
            }
            catch (CloudPatInvalidException ex)
            {
                _logger.LogWarning("daemon_auth: cloud rejected mcn_ token, path={Path}, error={Error}", context.Request.Path, ex.Message);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "invalid token" });
                return;
            }
            catch (CloudPatUnavailableException ex)
            {
                _logger.LogWarning("daemon_auth: cloud pat verify unavailable, path={Path}, error={Error}", context.Request.Path, ex.Message);
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { error = "cloud pat verifier unavailable" });
                return;
            }
        }

        // PAT fallback: "mul_" prefix
        if (token.StartsWith("mul_"))
        {
            var hash = TokenHasher.HashToken(token);

            // Check cache first
            var cachedUserId = await patCache.GetAsync(hash);
            if (cachedUserId is not null)
            {
                context.Request.Headers["X-User-ID"] = cachedUserId;
                context.Items["DaemonAuthPath"] = "pat";

                await _next(context);
                return;
            }

            // Cache miss: query database
            var pat = await db.PersonalAccessTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (pat is null)
            {
                _logger.LogWarning("daemon_auth: invalid PAT, path={Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "invalid token" });
                return;
            }

            var userId = pat.UserId.ToString();
            context.Request.Headers["X-User-ID"] = userId;

            // Cache with TTL clamped to token's remaining lifetime
            var ttl = PatCache.TTLForExpiry(DateTime.UtcNow, pat.ExpiresAt?.DateTime);
            await patCache.SetAsync(hash, userId, ttl);

            context.Items["DaemonAuthPath"] = "pat";

            await _next(context);
            return;
        }

        // JWT fallback
        var principal = jwtService.ValidateToken(token);
        if (principal is null)
        {
            _logger.LogWarning("daemon_auth: invalid JWT, path={Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid token" });
            return;
        }

        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub))
        {
            _logger.LogWarning("daemon_auth: invalid JWT claims, path={Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid claims" });
            return;
        }

        context.Request.Headers["X-User-ID"] = sub;
        context.Items["DaemonAuthPath"] = "jwt";

        await _next(context);
    }
}
