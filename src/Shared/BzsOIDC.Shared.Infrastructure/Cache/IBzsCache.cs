using System.Text.Json.Serialization.Metadata;

namespace BzsOIDC.Shared.Infrastructure.Cache;

public interface IBzsCache
{
    // 写入缓存；当 value 为 null 时等价于删除。
    void Set<T>(string key, T? value, EntryCacheOptions<T> options, IReadOnlyCollection<string>? tags = null);
    // 异步写入缓存；当 value 为 null 时等价于删除。
    Task SetAsync<T>(string key, T? value, EntryCacheOptions<T> options, IReadOnlyCollection<string>? tags = null);

    // 删除指定缓存键。
    void Remove(string key);
    // 异步删除指定缓存键。
    Task RemoveAsync(string key);
    // 读取缓存；若不存在或反序列化失败返回 null。
    T? Get<T>(string key, JsonTypeInfo<T?>? jsonTypeInfo);
    // 异步读取缓存；若不存在或反序列化失败返回 null。
    Task<T?> GetAsync<T>(string key, JsonTypeInfo<T?>? jsonTypeInfo);
    // 尝试读取缓存并返回是否命中。
    bool TryGet<T>(string key, out T? value, JsonTypeInfo<T?>? jsonTypeInfo);
    // 读穿透保护：并发 miss 时仅允许一个 factory 执行。
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        EntryCacheOptions<T> options,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default);
    // 对计数器键执行递增。
    Task<long> IncrementAsync(string key, long delta = 1, TimeSpan? absoluteExpirationRelativeToNow = null);
    // 按标签删除对应缓存项。
    Task RemoveByTagAsync(string tag);
}
