namespace BzsCenter.Shared.Infrastructure.Cache;

internal interface IBzsCacheStore
{
    // 写入底层缓存负载。
    void Set(string key, byte[] value, EntryCacheTimeOptions options, IReadOnlyCollection<string>? tags = null);
    // 异步写入底层缓存负载。
    Task SetAsync(string key, byte[] value, EntryCacheTimeOptions options, IReadOnlyCollection<string>? tags = null);

    // 删除指定缓存键。
    void Remove(string key);
    // 异步删除指定缓存键。
    Task RemoveAsync(string key);

    // 读取指定缓存键。
    byte[]? Get(string key);
    // 异步读取指定缓存键。
    Task<byte[]?> GetAsync(string key);
    // 尝试读取指定缓存键。
    bool TryGet(string key, out byte[]? value);

    // 对计数器键执行原子递增。
    Task<long> IncrementAsync(string key, long delta = 1, TimeSpan? absoluteExpirationRelativeToNow = null);

    // 按标签批量删除缓存键。
    Task RemoveByTagAsync(string tag);
}
