using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Multica.Core.Auth;

/// <summary>
/// Identity stored for daemon tokens (mdt_).
/// Uses short JSON keys (w, d) matching Go implementation.
/// </summary>
public record DaemonTokenIdentity
{
    public string WorkspaceId { get; init; } = "";
    public string DaemonId { get; init; } = "";
}

/// <summary>
/// Caches resolved daemon token (mdt_) lookups in Redis.
/// Null-safe — every method becomes a no-op when Redis is unavailable.
/// Compatible with Go's DaemonTokenCache implementation.
/// </summary>
public class DaemonTokenCache
{
    private const string KeyPrefix = "mul:auth:daemon:";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDatabase? _redis;
    private readonly ILogger<DaemonTokenCache> _logger;

    public DaemonTokenCache(IDatabase? redis, ILogger<DaemonTokenCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private static string KeyFor(string hash) => KeyPrefix + hash;

    /// <summary>
    /// Returns the cached identity for a token hash, or null on cache miss.
    /// </summary>
    public async Task<DaemonTokenIdentity?> GetAsync(string hash)
    {
        if (_redis is null) return null;

        try
        {
            var value = await _redis.StringGetAsync(KeyFor(hash));
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<DaemonTokenIdentity>(value.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "daemon_token_cache: get failed; falling back to DB");
            return null;
        }
    }

    /// <summary>
    /// Caches a token hash → identity mapping with the given TTL.
    /// Errors are logged and swallowed.
    /// </summary>
    public async Task SetAsync(string hash, DaemonTokenIdentity identity, TimeSpan? ttl = null)
    {
        if (_redis is null) return;

        var effectiveTtl = ttl ?? PatCache.DefaultTtl;
        if (effectiveTtl <= TimeSpan.Zero) return;

        try
        {
            var json = JsonSerializer.Serialize(identity, JsonOptions);
            await _redis.StringSetAsync(KeyFor(hash), json, effectiveTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "daemon_token_cache: set failed");
        }
    }

    /// <summary>
    /// Removes the cache entry for a token hash.
    /// </summary>
    public async Task InvalidateAsync(string hash)
    {
        if (_redis is null) return;

        try
        {
            await _redis.KeyDeleteAsync(KeyFor(hash));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "daemon_token_cache: invalidate failed; entry will expire on TTL");
        }
    }
}
