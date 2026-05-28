using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Multica.Core.Auth;

/// <summary>
/// Exception thrown when a cloud PAT is invalid.
/// </summary>
public class CloudPatInvalidException : Exception
{
    public string Reason { get; }

    public CloudPatInvalidException(string reason = "")
        : base($"cloud pat invalid{(string.IsNullOrEmpty(reason) ? "" : ": " + reason)}")
    {
        Reason = reason;
    }
}

/// <summary>
/// Exception thrown when the Fleet service is unavailable.
/// </summary>
public class CloudPatUnavailableException : Exception
{
    public CloudPatUnavailableException(string? message = null, Exception? inner = null)
        : base(message ?? "cloud pat verifier unavailable", inner)
    {
    }
}

/// <summary>
/// Verifies mcn_ (Multica Cloud Node) PATs by calling the Fleet service.
/// Caches positive results in Redis for 60 seconds.
/// Compatible with Go's CloudPATVerifier implementation.
/// </summary>
public class CloudPatVerifier
{
    private const string CacheKeyPrefix = "mul:auth:mcn:";
    private const string VerifyPath = "/api/v1/pat/verify";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly IDatabase? _redis;
    private readonly ILogger<CloudPatVerifier> _logger;
    private readonly string _fleetBaseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CloudPatVerifier(
        HttpClient http,
        IDatabase? redis,
        ILogger<CloudPatVerifier> logger,
        string fleetBaseUrl)
    {
        _http = http;
        _redis = redis;
        _logger = logger;
        _fleetBaseUrl = fleetBaseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Creates a CloudPatVerifier from configuration. Returns null if Fleet URL is not configured.
    /// </summary>
    public static CloudPatVerifier? Create(
        HttpClient http,
        IDatabase? redis,
        ILogger<CloudPatVerifier> logger,
        string? fleetBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(fleetBaseUrl))
            return null;

        return new CloudPatVerifier(http, redis, logger, fleetBaseUrl);
    }

    /// <summary>
    /// Verifies a cloud PAT token. Flow: Redis cache → Fleet API → owner lookup → cache result.
    /// </summary>
    public async Task<CloudPatIdentity> VerifyAsync(
        string token,
        Func<string, Task<bool>>? ownerLookup = null)
    {
        if (string.IsNullOrEmpty(token))
            throw new CloudPatInvalidException();

        var hash = TokenHasher.HashToken(token);

        // 1. Check cache
        var cached = await CacheGetAsync(hash);
        if (cached is not null)
            return cached;

        // 2. Call Fleet API
        var identity = await FetchFromFleetAsync(token);

        // 3. Owner existence check
        if (ownerLookup is not null)
        {
            try
            {
                var exists = await ownerLookup(identity.OwnerId);
                if (!exists)
                {
                    _logger.LogWarning("cloud_pat: cloud-verified owner_id has no local user {OwnerId}", identity.OwnerId);
                    throw new CloudPatInvalidException("owner_unknown");
                }
            }
            catch (CloudPatInvalidException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "cloud_pat: owner lookup failed; treating as unavailable");
                throw new CloudPatUnavailableException("owner lookup failed", ex);
            }
        }

        // 4. Cache positive result
        await CacheSetAsync(hash, identity);

        return identity;
    }

    private async Task<CloudPatIdentity> FetchFromFleetAsync(string token)
    {
        var request = new { token };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(
                $"{_fleetBaseUrl}{VerifyPath}",
                request,
                JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "cloud_pat: verify request failed");
            throw new CloudPatUnavailableException("fleet request failed", ex);
        }

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            var snippet = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("cloud_pat: verify returned {Status}: {Body}", response.StatusCode, snippet);
            throw new CloudPatUnavailableException($"fleet returned {(int)response.StatusCode}");
        }

        var parsed = await response.Content.ReadFromJsonAsync<FleetVerifyResponse>(JsonOptions);
        if (parsed is null)
            throw new CloudPatUnavailableException("failed to decode fleet response");

        if (!parsed.Valid)
            throw new CloudPatInvalidException(parsed.Reason ?? "");

        if (string.IsNullOrEmpty(parsed.OwnerId))
        {
            _logger.LogWarning("cloud_pat: verify returned valid=true with empty owner_id");
            throw new CloudPatUnavailableException("fleet returned empty owner_id");
        }

        return new CloudPatIdentity
        {
            OwnerId = parsed.OwnerId,
            InstanceId = parsed.InstanceId ?? "",
            InstanceRecordId = parsed.InstanceRecordId ?? ""
        };
    }

    private async Task<CloudPatIdentity?> CacheGetAsync(string hash)
    {
        if (_redis is null) return null;

        try
        {
            var value = await _redis.StringGetAsync(CacheKeyPrefix + hash);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<CloudPatIdentity>(value.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "cloud_pat: cache get failed; falling back to fleet");
            return null;
        }
    }

    private async Task CacheSetAsync(string hash, CloudPatIdentity identity)
    {
        if (_redis is null) return;

        try
        {
            var json = JsonSerializer.Serialize(identity, JsonOptions);
            await _redis.StringSetAsync(CacheKeyPrefix + hash, json, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "cloud_pat: cache set failed");
        }
    }

    private record FleetVerifyResponse
    {
        public bool Valid { get; init; }
        public string? Reason { get; init; }
        public string? OwnerId { get; init; }
        public string? InstanceId { get; init; }
        public string? InstanceRecordId { get; init; }
    }
}
