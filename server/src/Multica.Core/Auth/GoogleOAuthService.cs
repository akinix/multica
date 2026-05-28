using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Multica.Core.Auth;

/// <summary>
/// User information returned from Google OAuth.
/// </summary>
public record GoogleUserInfo
{
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public string Picture { get; init; } = "";
    public string Sub { get; init; } = "";
}

/// <summary>
/// Google OAuth 2.0 service for code exchange and user info retrieval.
/// Compatible with Go's Google OAuth implementation.
/// </summary>
public class GoogleOAuthService
{
    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly ILogger<GoogleOAuthService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GoogleOAuthService(
        HttpClient http,
        IConfiguration config,
        ILogger<GoogleOAuthService> logger)
    {
        _http = http;
        _clientId = config["GoogleOAuth:ClientId"] ?? "";
        _clientSecret = config["GoogleOAuth:ClientSecret"] ?? "";
        _logger = logger;
    }

    /// <summary>
    /// Exchanges an authorization code for an access token.
    /// </summary>
    public async Task<string?> ExchangeCodeAsync(string code, string redirectUri)
    {
        var request = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        };

        try
        {
            var response = await _http.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(request));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google OAuth code exchange failed: {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
            return result?.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google OAuth code exchange error");
            return null;
        }
    }

    /// <summary>
    /// Retrieves user information from Google using an access token.
    /// </summary>
    public async Task<GoogleUserInfo?> GetUserInfoAsync(string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google user info retrieval failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleUserInfo>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google user info retrieval error");
            return null;
        }
    }

    private record TokenResponse
    {
        public string? AccessToken { get; init; }
        public string? TokenType { get; init; }
        public int ExpiresIn { get; init; }
    }
}
