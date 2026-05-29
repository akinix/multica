using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Multica.Core.Auth;

/// <summary>
/// JWT token generation and validation using HMAC-SHA256.
/// Compatible with the Go backend's JWT implementation.
/// </summary>
public class JwtTokenService
{
    private readonly byte[] _secret;
    private readonly TimeSpan _tokenTtl;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtTokenService(IConfiguration config)
    {
        var secret = config["Jwt:Secret"] ?? "multica-dev-secret-change-in-production";
        _secret = Encoding.UTF8.GetBytes(secret);

        var ttlStr = config["Auth:TokenTTL"];
        _tokenTtl = string.IsNullOrEmpty(ttlStr)
            ? TimeSpan.FromDays(30)
            : TimeSpan.Parse(ttlStr);
    }

    /// <summary>
    /// Generates a JWT token with sub (userId) and email claims.
    /// </summary>
    public string GenerateToken(Guid userId, string email)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_secret),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(_tokenTtl),
            signingCredentials: credentials);

        return _handler.WriteToken(token);
    }

    /// <summary>
    /// Validates a JWT token and returns the ClaimsPrincipal, or null if invalid.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_secret),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        try
        {
            var principal = _handler.ValidateToken(token, parameters, out _);
            return principal;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
