using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Multica.Core.Auth;

/// <summary>
/// Caches resolved PAT lookups in Redis. Null-safe — every method becomes
/// a no-op when Redis is unavailable, degrading to direct DB lookups.
/// Compatible with Go's PATCache implementation.
/// </summary>
public class PatCache
{
    private const string KeyPrefix = "mul:auth:pat:";
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    private readonly IDatabase? _redis;
    private readonly ILogger<PatCache> _logger;

    public PatCache(IDatabase? redis, ILogger<PatCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private static string KeyFor(string hash) => KeyPrefix + hash;

    /// <summary>
    /// Returns the cached userId for a token hash, or null on cache miss.
    /// </summary>
    public async Task<string?> GetAsync(string hash)
    {
        if (_redis is null) return null;

        try
        {
            var value = await _redis.StringGetAsync(KeyFor(hash));
            if (value.HasValue)
                return value.ToString();

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pat_cache: get failed; falling back to DB");
            return null;
        }
    }

    /// <summary>
    /// Caches a token hash → userId mapping with the given TTL.
    /// Errors are logged and swallowed — cache write failure is not a request failure.
    /// </summary>
    public async Task SetAsync(string hash, string userId, TimeSpan? ttl = null)
    {
        if (_redis is null) return;

        var effectiveTtl = ttl ?? DefaultTtl;
        if (effectiveTtl <= TimeSpan.Zero) return;

        try
        {
            await _redis.StringSetAsync(KeyFor(hash), userId, effectiveTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pat_cache: set failed");
        }
    }

    /// <summary>
    /// Removes the cache entry for a token hash.
    /// Called on PAT revocation for immediate effect.
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
            _logger.LogWarning(ex, "pat_cache: invalidate failed; entry will expire on TTL");
        }
    }

    /// <summary>
    /// Computes the cache TTL for a token given its expiry.
    /// Returns min(defaultTtl, remaining lifetime), or 0 if expired.
    /// </summary>
    public static TimeSpan TTLForExpiry(DateTime now, DateTime? expiresAt)
    {
        if (!expiresAt.HasValue)
            return DefaultTtl;

        var remaining = expiresAt.Value - now;
        if (remaining <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return remaining < DefaultTtl ? remaining : DefaultTtl;
    }
}
