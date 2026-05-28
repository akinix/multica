using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Multica.Core.Auth;

/// <summary>
/// Cookie-based authentication service with CSRF protection.
/// Compatible with Go's cookie implementation.
/// </summary>
public class CookieService
{
    public const string AuthCookieName = "multica_auth";
    public const string CsrfCookieName = "multica_csrf";

    private readonly IConfiguration _config;
    private readonly ILogger<CookieService> _logger;

    public CookieService(IConfiguration config, ILogger<CookieService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Sets the HttpOnly auth cookie and the readable CSRF cookie on the response.
    /// </summary>
    public void SetAuthCookies(HttpResponse response, string token)
    {
        var secure = IsSecureCookie();
        var domain = CookieDomain();
        var ttl = AuthTokenTTL();
        var expires = DateTimeOffset.UtcNow.Add(ttl);

        // Auth cookie (HttpOnly)
        response.Cookies.Append(AuthCookieName, token, new CookieOptions
        {
            Path = "/",
            Domain = domain,
            MaxAge = ttl,
            Expires = expires,
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict
        });

        // CSRF cookie (not HttpOnly — readable by JavaScript)
        var csrfToken = GenerateCsrfToken(token);
        response.Cookies.Append(CsrfCookieName, csrfToken, new CookieOptions
        {
            Path = "/",
            Domain = domain,
            MaxAge = ttl,
            Expires = expires,
            HttpOnly = false,
            Secure = secure,
            SameSite = SameSiteMode.Strict
        });
    }

    /// <summary>
    /// Removes the auth and CSRF cookies.
    /// </summary>
    public void ClearAuthCookies(HttpResponse response)
    {
        var domain = CookieDomain();
        var secure = IsSecureCookie();

        response.Cookies.Delete(AuthCookieName, new CookieOptions
        {
            Path = "/",
            Domain = domain,
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict
        });

        response.Cookies.Delete(CsrfCookieName, new CookieOptions
        {
            Path = "/",
            Domain = domain,
            HttpOnly = false,
            Secure = secure,
            SameSite = SameSiteMode.Strict
        });
    }

    /// <summary>
    /// Generates a CSRF token bound to the auth token via HMAC.
    /// Format: hex(nonce) + "." + hex(HMAC-SHA256(nonce, authToken)).
    /// </summary>
    public static string GenerateCsrfToken(string authToken)
    {
        var nonce = RandomNumberGenerator.GetBytes(16);
        var nonceHex = Convert.ToHexString(nonce).ToLowerInvariant();

        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(authToken), nonce);
        var sigHex = Convert.ToHexString(mac).ToLowerInvariant();

        return nonceHex + "." + sigHex;
    }

    /// <summary>
    /// Returns the configured auth token lifetime (default 30 days).
    /// </summary>
    public TimeSpan AuthTokenTTL()
    {
        var ttlStr = _config["Auth:TokenTTL"];
        if (!string.IsNullOrEmpty(ttlStr) && TimeSpan.TryParse(ttlStr, out var ttl))
            return ttl;

        return TimeSpan.FromDays(30);
    }

    /// <summary>
    /// Returns whether cookies should use the Secure flag.
    /// Derived from the scheme of FRONTEND_ORIGIN.
    /// </summary>
    public bool IsSecureCookie()
    {
        var frontendOrigin = _config["Auth:FrontendOrigin"] ?? "";
        if (string.IsNullOrWhiteSpace(frontendOrigin))
            return false;

        return frontendOrigin.StartsWith("https", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the cookie domain from configuration.
    /// Returns empty string if it looks like an IP address.
    /// </summary>
    public string CookieDomain()
    {
        var domain = _config["Auth:CookieDomain"] ?? "";
        domain = domain.Trim();

        if (string.IsNullOrEmpty(domain))
            return "";

        // Check if it's an IP address
        var cleanDomain = domain.TrimStart('.');
        if (System.Net.IPAddress.TryParse(cleanDomain, out _))
        {
            _logger.LogWarning("COOKIE_DOMAIN looks like an IP address; ignoring. RFC 6265 forbids IP literals in the cookie Domain attribute.");
            return "";
        }

        return domain;
    }
}
