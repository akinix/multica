using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Multica.Core.Auth;

/// <summary>
/// Validates CSRF tokens for cookie-based authentication.
/// Compatible with Go's ValidateCSRF implementation.
/// </summary>
public static class CsrfValidator
{
    /// <summary>
    /// Validates the X-CSRF-Token header against the auth cookie.
    /// Returns true if validation passes (including for safe methods).
    /// </summary>
    public static bool Validate(HttpRequest request)
    {
        // Safe methods don't need CSRF validation
        var method = request.Method;
        if (method == "GET" || method == "HEAD" || method == "OPTIONS")
            return true;

        // Get CSRF token from header
        var csrfHeader = request.Headers["X-CSRF-Token"].ToString();
        if (string.IsNullOrEmpty(csrfHeader))
            return false;

        // Get auth token from cookie
        if (!request.Cookies.TryGetValue(CookieService.AuthCookieName, out var authToken) ||
            string.IsNullOrEmpty(authToken))
            return false;

        // Parse CSRF token: hex(nonce).hex(signature)
        var parts = csrfHeader.Split('.', 2);
        if (parts.Length != 2)
            return false;

        byte[] nonce, expectedSig;
        try
        {
            nonce = Convert.FromHexString(parts[0]);
            expectedSig = Convert.FromHexString(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        // Verify HMAC-SHA256(nonce, authToken) == signature
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(authToken), nonce);
        return CryptographicOperations.FixedTimeEquals(mac, expectedSig);
    }
}
