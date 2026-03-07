using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BzsCenter.Shared.Infrastructure.Cache;

public static class CacheServiceExtensions
{
    extension(IServiceCollection sc)
    {
        // 注册 BzsCache 门面与底层实现，并接入 OTEL。
        public IServiceCollection AddBzsCache(IConfiguration configuration)
        {
            sc.AddOptions<CacheOptions>().Bind(configuration.GetSection(CacheOptions.SectionName));
            var options = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();

            sc.AddMemoryCache();

            switch (options.CacheType)
            {
                case CacheType.Memory:
                    sc.AddSingleton<IBzsCacheStore, MemoryBzsCacheStore>();
                    break;
                case CacheType.Redis:
                    if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
                    {
                        throw new InvalidOperationException(
                            $"{CacheOptions.SectionName}:RedisConnectionString is required when CacheType is Redis.");
                    }

                    sc.AddSingleton<IConnectionMultiplexer>(
                        _ => ConnectionMultiplexer.Connect(CreateRedisConfiguration(options.RedisConnectionString)));
                    sc.AddSingleton<IBzsCacheStore, RedisBzsCacheStore>();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported cache type '{options.CacheType}'.");
            }

            sc.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddSource(LoggingBzsCacheDecorator.ActivitySourceName))
                .WithMetrics(metrics => metrics.AddMeter(LoggingBzsCacheDecorator.MeterName));

            sc.AddSingleton<BzsCacheCore>();
            sc.AddSingleton<IBzsCache>(sp =>
            {
                var core = sp.GetRequiredService<BzsCacheCore>();
                var logger = sp.GetRequiredService<ILogger<LoggingBzsCacheDecorator>>();
                var cacheOpts = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
                return new LoggingBzsCacheDecorator(core, logger, cacheOpts);
            });

            return sc;
        }
    }

    // 构建更稳健的 Redis 连接配置。
    private static ConfigurationOptions CreateRedisConfiguration(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString, ignoreUnknown: true);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = Math.Max(options.ConnectRetry, 3);
        options.KeepAlive = Math.Max(options.KeepAlive, 15);
        return options;
    }
}
