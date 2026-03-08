using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace BzsCenter.Shared.Infrastructure.Cache;

internal sealed class BzsCacheCore(IBzsCacheStore store, IOptions<CacheOptions> cacheOptions) : IBzsCache
{
    private static readonly Meter _meter = new(LoggingBzsCacheDecorator.MeterName);
    private static readonly Histogram<long> _payloadSizeBytes = _meter.CreateHistogram<long>("cache.payload.size", "By");

    private static readonly Counter<long> _deserializeFailureCounter =
        _meter.CreateCounter<long>("cache.deserialize.failure.count");

    private static readonly Counter<long> _stampedeJoinCounter = _meter.CreateCounter<long>("cache.stampede.join.count");

    private static readonly UpDownCounter<long> _stampedeActiveCounter =
        _meter.CreateUpDownCounter<long>("cache.stampede.active");

    private readonly string _cacheSystem = cacheOptions.Value.CacheType == CacheType.Redis ? "redis" : "inmemory";

    private readonly Lock _keyLockMapGuard = new();
    private readonly Dictionary<string, KeyLockEntry> _keyLocks = [];

    // 同步写入缓存值（null 视为删除）。
    public void Set<T>(string key, T? value, EntryCacheOptions<T> options, IReadOnlyCollection<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(options);

        if (value is null)
        {
            store.Remove(key);
            return;
        }

        var payload = Serialize(value, options.JsonTypeInfo);
        _payloadSizeBytes.Record(payload.LongLength,
            new KeyValuePair<string, object?>("operation", "set"),
            new KeyValuePair<string, object?>("cache.system", _cacheSystem));
        store.Set(key, payload, options.EntryCacheTimeOptions, tags);
    }

    // 异步写入缓存值（null 视为删除）。
    public Task SetAsync<T>(string key, T? value, EntryCacheOptions<T> options,
        IReadOnlyCollection<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(options);

        if (value is null)
        {
            return store.RemoveAsync(key);
        }

        var payload = Serialize(value, options.JsonTypeInfo);
        _payloadSizeBytes.Record(payload.LongLength,
            new KeyValuePair<string, object?>("operation", "set"),
            new KeyValuePair<string, object?>("cache.system", _cacheSystem));
        return store.SetAsync(key, payload, options.EntryCacheTimeOptions, tags);
    }

    // 同步删除缓存键。
    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        store.Remove(key);
    }

    // 异步删除缓存键。
    public Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return store.RemoveAsync(key);
    }

    // 同步读取缓存并执行安全反序列化。
    public T? Get<T>(string key, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var payload = store.Get(key);
        if (TrySafeDeserialize(payload, jsonTypeInfo, out var value))
        {
            return value;
        }

        _deserializeFailureCounter.Add(1,
            new KeyValuePair<string, object?>("operation", "get"),
            new KeyValuePair<string, object?>("cache.system", _cacheSystem));
        // 读到损坏数据时主动清理，避免后续重复反序列化失败。
        store.Remove(key);
        return default;
    }

    // 异步读取缓存并执行安全反序列化。
    public async Task<T?> GetAsync<T>(string key, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var payload = await store.GetAsync(key);
        if (TrySafeDeserialize(payload, jsonTypeInfo, out var value))
        {
            return value;
        }

        _deserializeFailureCounter.Add(1,
            new KeyValuePair<string, object?>("operation", "get"),
            new KeyValuePair<string, object?>("cache.system", _cacheSystem));
        await store.RemoveAsync(key);
        return default;
    }

    // 尝试读取缓存，失败时返回 false。
    public bool TryGet<T>(string key, out T? value, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!store.TryGet(key, out var payload) || payload is null)
        {
            value = default;
            return false;
        }

        if (!TrySafeDeserialize(payload, jsonTypeInfo, out value))
        {
            // 与 Get 保持一致：发现坏数据即剔除。
            _deserializeFailureCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "try_get"),
                new KeyValuePair<string, object?>("cache.system", _cacheSystem));
            store.Remove(key);
            value = default;
            return false;
        }

        return value is not null;
    }

    // 读取或创建缓存值，内置单进程并发击穿保护。
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        EntryCacheOptions<T> options,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);

        var cached = await GetAsync(key, options.JsonTypeInfo);
        if (cached is not null)
        {
            return cached;
        }

        // Given multiple concurrent misses, when one factory starts, then others wait on the same key lock.
        // This lock is process-local and protects single-node stampede scenarios.
        var keyLockEntry = GetOrAddKeyLock(key);
        var lockAcquired = false;
        try
        {
            await keyLockEntry.Semaphore.WaitAsync(cancellationToken);
            lockAcquired = true;

            cached = await GetAsync(key, options.JsonTypeInfo);
            if (cached is not null)
            {
                return cached;
            }

            T created;
            // Given a newly created value, when factory succeeds, then cache is populated before returning.
            _stampedeActiveCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "get_or_create"),
                new KeyValuePair<string, object?>("cache.system", _cacheSystem));
            try
            {
                created = await factory(cancellationToken);
            }
            finally
            {
                _stampedeActiveCounter.Add(-1,
                    new KeyValuePair<string, object?>("operation", "get_or_create"),
                    new KeyValuePair<string, object?>("cache.system", _cacheSystem));
            }

            await SetAsync(key, created, options, tags);
            return created;
        }
        finally
        {
            if (lockAcquired)
            {
                keyLockEntry.Semaphore.Release();
            }

            ReleaseKeyLock(key, keyLockEntry);
        }
    }

    // 对底层计数器执行递增。
    public Task<long> IncrementAsync(string key, long delta = 1, TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return store.IncrementAsync(key, delta, absoluteExpirationRelativeToNow);
    }

    // 按标签删除缓存。
    public Task RemoveByTagAsync(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return store.RemoveByTagAsync(tag);
    }

    // 将对象序列化为 UTF-8 字节数组。
    private static byte[] Serialize<T>(T value, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        return jsonTypeInfo is null
            ? JsonSerializer.SerializeToUtf8Bytes(value)
            : JsonSerializer.SerializeToUtf8Bytes(value, jsonTypeInfo);
    }

    // 将 UTF-8 字节数组反序列化为目标类型。
    private static T? Deserialize<T>(byte[]? payload, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        if (payload is null)
        {
            return default;
        }

        return jsonTypeInfo is null
            ? JsonSerializer.Deserialize<T>(payload)
            : JsonSerializer.Deserialize(payload, jsonTypeInfo);
    }

    // 安全反序列化：屏蔽 JsonException 并返回失败标记。
    private static bool TrySafeDeserialize<T>(byte[]? payload, JsonTypeInfo<T?>? jsonTypeInfo, out T? value)
    {
        try
        {
            value = Deserialize(payload, jsonTypeInfo);
            return true;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }

    // 获取或创建某个 key 的并发锁条目。
    private KeyLockEntry GetOrAddKeyLock(string key)
    {
        lock (_keyLockMapGuard)
        {
            if (_keyLocks.TryGetValue(key, out var existing))
            {
                existing.RefCount++;
                _stampedeJoinCounter.Add(1,
                    new KeyValuePair<string, object?>("operation", "get_or_create"),
                    new KeyValuePair<string, object?>("cache.system", _cacheSystem));
                return existing;
            }

            var created = new KeyLockEntry();
            _keyLocks[key] = created;
            return created;
        }
    }

    // 释放并在引用归零时回收锁条目。
    private void ReleaseKeyLock(string key, KeyLockEntry keyLockEntry)
    {
        lock (_keyLockMapGuard)
        {
            if (_keyLocks.TryGetValue(key, out var existing)
                && ReferenceEquals(existing, keyLockEntry))
            {
                keyLockEntry.RefCount--;
                if (keyLockEntry.RefCount == 0)
                {
                    _keyLocks.Remove(key);
                    keyLockEntry.Semaphore.Dispose();
                }
            }
        }
    }

    // key 级并发锁容器。
    private sealed class KeyLockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount { get; set; } = 1;
    }
}