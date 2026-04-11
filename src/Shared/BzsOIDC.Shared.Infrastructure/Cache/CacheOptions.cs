using System.Text.Json.Serialization.Metadata;

namespace BzsOIDC.Shared.Infrastructure.Cache;

public sealed class CacheOptions
{
    // 配置节名称：用于从 IConfiguration 绑定缓存配置。
    public const string SectionName = "CacheOptions";
    // 当前缓存实现类型（内存或 Redis）。
    public CacheType CacheType { get; init; } = CacheType.Memory;
    // Redis 连接串，仅在 CacheType=Redis 时必填。
    public string? RedisConnectionString { get; init; }
    // Redis 实例名前缀，用于隔离不同应用。
    public string RedisInstanceName { get; init; } = "bzsoidc:";
    // 业务缓存键前缀，用于统一命名和排查。
    public string KeyPrefix { get; init; } = "bzs:";
}

public enum CacheType
{
    Memory,
    Redis
}

public record EntryCacheTimeOptions(TimeSpan AbsoluteExpirationRelativeToNow, TimeSpan? SlidingExpiration);

public class EntryCacheOptions<T>
{
    // 构造单条缓存项配置。
    private EntryCacheOptions(EntryCacheTimeOptions entryCacheTimeOptions, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        EntryCacheTimeOptions = entryCacheTimeOptions;
        JsonTypeInfo = jsonTypeInfo;
    }


    public JsonTypeInfo<T?>? JsonTypeInfo { get; init; }
    // 统一缓存过期策略（绝对过期 + 可选滑动过期）。
    public EntryCacheTimeOptions EntryCacheTimeOptions { get; init; }

    // 快速创建缓存项配置。
    public static EntryCacheOptions<T> Create(TimeSpan absoluteExpirationRelativeToNow, TimeSpan? slidingExpiration = null,
        JsonTypeInfo<T?>? jsonTypeInfo = null)
    {
        return new EntryCacheOptions<T>(new EntryCacheTimeOptions(absoluteExpirationRelativeToNow, slidingExpiration),
            jsonTypeInfo);
    }
}
