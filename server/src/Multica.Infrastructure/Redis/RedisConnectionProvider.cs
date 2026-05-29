using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Multica.Infrastructure.Redis;

/// <summary>
/// Provides Redis connection and database instances.
/// Handles connection failures gracefully with logging.
/// </summary>
public class RedisConnectionProvider : IDisposable
{
    private readonly Lazy<Task<IConnectionMultiplexer>> _lazyConnection;
    private readonly ILogger<RedisConnectionProvider> _logger;
    private readonly string _connectionString;

    public RedisConnectionProvider(IConfiguration configuration, ILogger<RedisConnectionProvider> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        _lazyConnection = new Lazy<Task<IConnectionMultiplexer>>(ConnectAsync);
    }

    /// <summary>
    /// Gets the Redis connection multiplexer.
    /// </summary>
    public IConnectionMultiplexer Connection => _lazyConnection.Value.GetAwaiter().GetResult();

    /// <summary>
    /// Gets the default Redis database.
    /// </summary>
    public IDatabase Store => Connection.GetDatabase();

    private async Task<IConnectionMultiplexer> ConnectAsync()
    {
        try
        {
            var options = ConfigurationOptions.Parse(_connectionString);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5000;

            var connection = await ConnectionMultiplexer.ConnectAsync(options);

            connection.ConnectionFailed += (sender, args) =>
            {
                _logger.LogWarning("Redis connection failed: {FailureType}", args.FailureType);
            };

            connection.ConnectionRestored += (sender, args) =>
            {
                _logger.LogInformation("Redis connection restored");
            };

            _logger.LogInformation("Redis connected to {Endpoint}", _connectionString);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis at {ConnectionString}", _connectionString);
            throw;
        }
    }

    public void Dispose()
    {
        if (_lazyConnection.IsValueCreated)
        {
            Connection?.Dispose();
        }
    }
}
