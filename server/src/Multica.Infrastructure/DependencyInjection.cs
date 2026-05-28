using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Multica.Core.Auth;
using Multica.Infrastructure.Auth;
using Multica.Infrastructure.Data;
using Multica.Infrastructure.Redis;
using Npgsql;
using StackExchange.Redis;

namespace Multica.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddDbContextPool<MulticaDbContext>(options =>
            options
                .UseNpgsql(dataSource, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    npgsqlOptions.CommandTimeout(30);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                })
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        );

        services.AddSingleton<RedisConnectionProvider>();
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            sp.GetRequiredService<RedisConnectionProvider>().Connection);
        services.AddSingleton<IDatabase>(sp =>
            sp.GetRequiredService<RedisConnectionProvider>().Store);

        // Auth services
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<PatCache>();
        services.AddSingleton<DaemonTokenCache>();
        services.AddScoped<TaskTokenValidator>();

        // Cloud PAT verifier (null if not configured)
        services.AddSingleton<CloudPatVerifier>(sp =>
        {
            var redis = sp.GetService<IDatabase>();
            var logger = sp.GetRequiredService<ILogger<CloudPatVerifier>>();
            var fleetBaseUrl = config["CloudFront:FleetBaseUrl"];

            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            return CloudPatVerifier.Create(http, redis, logger, fleetBaseUrl)
                ?? new CloudPatVerifier(http, redis, logger, "");
        });

        services.AddHealthChecks()
            .AddNpgSql(
                sp => sp.GetRequiredService<NpgsqlDataSource>(),
                name: "postgresql",
                tags: ["ready"])
            .AddRedis(
                sp => sp.GetRequiredService<IConnectionMultiplexer>(),
                name: "redis",
                tags: ["ready"]);

        return services;
    }
}
