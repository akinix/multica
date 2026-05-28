using System.Security.Cryptography;
using System.Text;

namespace Multica.Core.Auth;

/// <summary>
/// Provides SHA-256 token hashing compatible with the Go backend's auth.HashToken.
/// </summary>
public static class TokenHasher
{
    /// <summary>
    /// Returns the hex-encoded SHA-256 hash of a token string.
    /// Must match Go's hex.EncodeToString(sha256.Sum256([]byte(token))).
    /// </summary>
    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
