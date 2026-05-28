using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Multica.Core.Auth;

/// <summary>
/// Generates signed cookies and URLs for CloudFront private distributions.
/// Compatible with Go's CloudFrontSigner implementation.
/// </summary>
public class CloudFrontSigner
{
    private readonly string _keyPairId;
    private readonly RSA _privateKey;
    private readonly string _domain;
    private readonly string _cookieDomain;

    public CloudFrontSigner(string keyPairId, RSA privateKey, string domain, string cookieDomain)
    {
        _keyPairId = keyPairId;
        _privateKey = privateKey;
        _domain = domain;
        _cookieDomain = cookieDomain;
    }

    /// <summary>
    /// Creates a CloudFrontSigner from configuration. Returns null if not configured.
    /// </summary>
    public static async Task<CloudFrontSigner?> FromConfigurationAsync(
        IConfiguration config,
        ILogger logger)
    {
        var keyPairId = config["CloudFront:KeyPairId"];
        if (string.IsNullOrEmpty(keyPairId))
        {
            logger.LogInformation("CLOUDFRONT_KEY_PAIR_ID not set, signed cookies disabled");
            return null;
        }

        var domain = config["CloudFront:Domain"];
        if (string.IsNullOrEmpty(domain))
        {
            logger.LogError("CLOUDFRONT_DOMAIN not set");
            return null;
        }

        var cookieDomain = config["Auth:CookieDomain"] ?? "";
        if (string.IsNullOrEmpty(cookieDomain))
        {
            logger.LogError("COOKIE_DOMAIN not set");
            return null;
        }

        var privateKey = await LoadPrivateKeyAsync(config, logger);
        if (privateKey is null)
            return null;

        logger.LogInformation("CloudFront cookie signer initialized: {KeyPairId}, {Domain}", keyPairId, domain);
        return new CloudFrontSigner(keyPairId, privateKey, domain, cookieDomain);
    }

    private static async Task<RSA?> LoadPrivateKeyAsync(IConfiguration config, ILogger logger)
    {
        // 1. Try Secrets Manager
        var secretName = config["CloudFront:PrivateKeySecret"];
        if (!string.IsNullOrEmpty(secretName))
        {
            logger.LogInformation("Loading CloudFront private key from Secrets Manager: {Secret}", secretName);
            // TODO: Implement AWS Secrets Manager integration
            // For now, fall through to env var
        }

        // 2. Fallback: base64-encoded env var (local dev)
        var pkB64 = config["CLOUDFRONT_PRIVATE_KEY"];
        if (!string.IsNullOrEmpty(pkB64))
        {
            logger.LogInformation("Loading CloudFront private key from environment variable (local dev)");
            try
            {
                var pemBytes = Convert.FromBase64String(pkB64);
                return ParseRSAPrivateKey(pemBytes);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse CloudFront private key");
                return null;
            }
        }

        logger.LogError("Neither CLOUDFRONT_PRIVATE_KEY_SECRET nor CLOUDFRONT_PRIVATE_KEY is set");
        return null;
    }

    private static RSA ParseRSAPrivateKey(byte[] pemBytes)
    {
        var rsa = RSA.Create();

        // Try importing from PEM
        try
        {
            rsa.ImportFromPem(Encoding.UTF8.GetString(pemBytes));
            return rsa;
        }
        catch
        {
            // Fallback: try importing as PKCS8/PKCS1
            rsa.ImportPkcs8PrivateKey(pemBytes, out _);
            return rsa;
        }
    }

    /// <summary>
    /// Generates the three CloudFront signed cookies with the given expiry.
    /// </summary>
    public List<CookieHeaderValue> SignedCookies(DateTime expiry)
    {
        var epochTime = new DateTimeOffset(expiry).ToUnixTimeSeconds();
        var policy = $"{{\"Statement\":[{{\"Resource\":\"https://{_domain}/*\",\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{epochTime}}}}}}}]}}";

        var encodedPolicy = CfBase64Encode(Encoding.UTF8.GetBytes(policy));
        var signature = _privateKey.SignData(
            Encoding.UTF8.GetBytes(policy),
            HashAlgorithmName.SHA1,
            RSASignaturePadding.Pkcs1);
        var encodedSig = CfBase64Encode(signature);

        var cookieAttrs = (string name, string value) => new CookieHeaderValue
        {
            Name = name,
            Value = value,
            Domain = _cookieDomain,
            Path = "/",
            Expires = expiry,
            Secure = true,
            HttpOnly = true,
            SameSite = "None"
        };

        return
        [
            cookieAttrs("CloudFront-Policy", encodedPolicy),
            cookieAttrs("CloudFront-Signature", encodedSig),
            cookieAttrs("CloudFront-Key-Pair-Id", _keyPairId)
        ];
    }

    /// <summary>
    /// Generates a CloudFront signed URL for the given resource URL.
    /// </summary>
    public string SignedURL(string resourceUrl, DateTime expiry)
    {
        var epochTime = new DateTimeOffset(expiry).ToUnixTimeSeconds();
        var policy = $"{{\"Statement\":[{{\"Resource\":\"{resourceUrl}\",\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{epochTime}}}}}}}]}}";

        var encodedPolicy = CfBase64Encode(Encoding.UTF8.GetBytes(policy));
        var signature = _privateKey.SignData(
            Encoding.UTF8.GetBytes(policy),
            HashAlgorithmName.SHA1,
            RSASignaturePadding.Pkcs1);
        var encodedSig = CfBase64Encode(signature);

        var separator = resourceUrl.Contains('?') ? "&" : "?";
        return $"{resourceUrl}{separator}Policy={encodedPolicy}&Signature={encodedSig}&Key-Pair-Id={_keyPairId}";
    }

    /// <summary>
    /// Applies CloudFront's URL-safe base64 encoding.
    /// </summary>
    private static string CfBase64Encode(byte[] data)
    {
        var encoded = Convert.ToBase64String(data);
        return encoded
            .Replace("+", "-")
            .Replace("=", "_")
            .Replace("/", "~");
    }

    /// <summary>
    /// Represents a CloudFront signed cookie header value.
    /// </summary>
    public record CookieHeaderValue
    {
        public string Name { get; init; } = "";
        public string Value { get; init; } = "";
        public string Domain { get; init; } = "";
        public string Path { get; init; } = "/";
        public DateTime Expires { get; init; }
        public bool Secure { get; init; }
        public bool HttpOnly { get; init; }
        public string SameSite { get; init; } = "None";
    }
}
