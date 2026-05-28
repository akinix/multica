using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Auth;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Authentication API endpoints.
/// Compatible with Go's auth handler implementation.
/// </summary>
public static class AuthHandler
{
    /// <summary>
    /// Maps auth-related endpoints to the application.
    /// </summary>
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/google/callback", GoogleCallback);
        group.MapPost("/logout", Logout);
        group.MapGet("/me", GetCurrentUser);
    }

    /// <summary>
    /// Google OAuth callback: exchanges code for token, creates/finds user, sets cookies.
    /// </summary>
    private static async Task<IResult> GoogleCallback(
        [FromBody] GoogleCallbackRequest request,
        HttpContext httpContext,
        GoogleOAuthService googleOAuth,
        JwtTokenService jwtService,
        CookieService cookieService,
        SignupControlService signupControl,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        // Exchange code for access token
        var accessToken = await googleOAuth.ExchangeCodeAsync(request.Code, request.RedirectUri);
        if (accessToken is null)
            return Results.BadRequest(new { error = "failed to exchange code" });

        // Get user info from Google
        var userInfo = await googleOAuth.GetUserInfoAsync(accessToken);
        if (userInfo is null)
            return Results.BadRequest(new { error = "failed to get user info" });

        // Find or create user
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == userInfo.Email);

        if (user is null)
        {
            // Check signup controls
            if (!signupControl.IsSignupAllowed(userInfo.Email))
            {
                return Results.Forbid();
            }

            user = new User
            {
                Id = Guid.NewGuid(),
                Email = userInfo.Email,
                Name = userInfo.Name,
                AvatarUrl = userInfo.Picture,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        else
        {
            // Update user info
            user.Name = userInfo.Name;
            user.AvatarUrl = userInfo.Picture;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        // Generate JWT token
        var token = jwtService.GenerateToken(user.Id, user.Email);

        // Set cookies
        cookieService.SetAuthCookies(httpContext.Response, token);

        return Results.Ok(new
        {
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = user.Name,
                picture = user.AvatarUrl
            }
        });
    }

    /// <summary>
    /// Logout: clears auth cookies.
    /// </summary>
    private static IResult Logout(CookieService cookieService, HttpContext context)
    {
        cookieService.ClearAuthCookies(context.Response);
        return Results.Ok(new { ok = true });
    }

    /// <summary>
    /// Get current user info from X-User-ID header.
    /// </summary>
    private static async Task<IResult> GetCurrentUser(
        HttpContext context,
        MulticaDbContext db)
    {
        var userIdStr = context.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return Results.NotFound(new { error = "user not found" });

        return Results.Ok(new
        {
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = user.Name,
                picture = user.AvatarUrl
            }
        });
    }

    /// <summary>
    /// Request body for Google OAuth callback.
    /// </summary>
    public record GoogleCallbackRequest
    {
        public string Code { get; init; } = "";
        public string RedirectUri { get; init; } = "";
    }
}
