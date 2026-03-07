using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace BzsCenter.Shared.Infrastructure.Cache;

internal sealed class MemoryBzsCacheStore(IMemoryCache memoryCache) : IBzsCacheStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _keysByTag = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tagsByKey = new();
    private readonly ConcurrentDictionary<string, object> _tagLocks = new();
    private readonly ConcurrentDictionary<string, object> _counterLocks = new();
    private static readonly byte _placeholder = 0;
    private const string CounterPrefix = "counter::";

    // 同步写入内存缓存并维护标签索引。
    public void Set(string key, byte[] value, EntryCacheTimeOptions options, IReadOnlyCollection<string>? tags = null)
    {
        var sync = GetTagLock(key);
        lock (sync)
        {
            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow,
                SlidingExpiration = options.SlidingExpiration
            };
            entryOptions.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
            {
                EvictionCallback = (_, _, _, state) =>
                {
                    if (state is string k)
                    {
                        RemoveKeyTagIndex(k);
                    }
                },
                State = key
            });

            memoryCache.Set(key, value, entryOptions);
            ReplaceTags(key, tags);
        }

        TryRemoveLock(_tagLocks, key, sync);
    }

    // 异步写入（内存实现复用同步路径）。
    public Task SetAsync(string key, byte[] value, EntryCacheTimeOptions options, IReadOnlyCollection<string>? tags = null)
    {
        Set(key, value, options, tags);
        return Task.CompletedTask;
    }

    // 同步删除缓存并移除标签索引。
    public void Remove(string key)
    {
        var sync = GetTagLock(key);
        lock (sync)
        {
            memoryCache.Remove(key);
            RemoveKeyTagIndex(key);
        }

        TryRemoveLock(_tagLocks, key, sync);
    }

    // 异步删除（内存实现复用同步路径）。
    public Task RemoveAsync(string key)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    // 同步读取缓存。
    public byte[]? Get(string key)
    {
        return memoryCache.TryGetValue<byte[]>(key, out var value) ? value : null;
    }

    // 异步读取缓存（内存实现直接返回）。
    public Task<byte[]?> GetAsync(string key)
    {
        return Task.FromResult(Get(key));
    }

    // 尝试读取缓存并返回命中标记。
    public bool TryGet(string key, out byte[]? value)
    {
        return memoryCache.TryGetValue<byte[]>(key, out value);
    }

    // 对内存计数器键递增。
    public Task<long> IncrementAsync(string key, long delta = 1, TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        var counterKey = CounterCacheKey(key);
        // 计数器使用独立锁，避免全局锁导致热点互相阻塞。
        var sync = _counterLocks.GetOrAdd(counterKey, _ => new object());
        long next;

        lock (sync)
        {
            var current = 0L;
            if (memoryCache.TryGetValue<byte[]>(counterKey, out var value) && value is not null)
            {
                if (!long.TryParse(System.Text.Encoding.UTF8.GetString(value), out current))
                {
                    throw new InvalidOperationException($"Counter value for key '{key}' is not an integer.");
                }
            }

            next = checked(current + delta);
            var ttl = absoluteExpirationRelativeToNow ?? TimeSpan.FromMinutes(10);

            memoryCache.Set(counterKey,
                System.Text.Encoding.UTF8.GetBytes(next.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });

        }

        TryRemoveLock(_counterLocks, counterKey, sync);
        return Task.FromResult(next);
    }

    // 按标签批量删除缓存项。
    public Task RemoveByTagAsync(string tag)
    {
        if (!_keysByTag.TryRemove(tag, out var keys))
        {
            return Task.CompletedTask;
        }

        foreach (var key in keys.Keys)
        {
            // 删除时复用 key 级锁，降低与并发 Set/Remove 的索引竞争。
            var sync = GetTagLock(key);
            lock (sync)
            {
                memoryCache.Remove(key);
                RemoveKeyTagIndex(key);
            }

            TryRemoveLock(_tagLocks, key, sync);
        }

        return Task.CompletedTask;
    }

    // 用新标签替换旧标签索引。
    private void ReplaceTags(string key, IReadOnlyCollection<string>? tags)
    {
        RemoveKeyTagIndex(key);

        if (tags is null || tags.Count == 0)
        {
            return;
        }

        var keyTags = _tagsByKey.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>());
        foreach (var tag in tags.Where(static t => !string.IsNullOrWhiteSpace(t)))
        {
            keyTags[tag] = _placeholder;
            var keys = _keysByTag.GetOrAdd(tag, _ => new ConcurrentDictionary<string, byte>());
            keys[key] = _placeholder;
        }
    }

    // 移除指定 key 对应的标签反向索引。
    private void RemoveKeyTagIndex(string key)
    {
        if (!_tagsByKey.TryRemove(key, out var tags))
        {
            return;
        }

        foreach (var tag in tags.Keys)
        {
            if (!_keysByTag.TryGetValue(tag, out var keys))
            {
                continue;
            }

            _ = keys.TryRemove(key, out _);
            if (keys.IsEmpty)
            {
                _ = _keysByTag.TryRemove(tag, out _);
            }
        }
    }

    // 生成计数器专用缓存键。
    private static string CounterCacheKey(string key)
    {
        return $"{CounterPrefix}{key}";
    }

    // 获取 key 对应的并发锁。
    private object GetTagLock(string key)
    {
        return _tagLocks.GetOrAdd(key, _ => new object());
    }

    // 尝试回收字典中的锁对象。
    private static void TryRemoveLock(ConcurrentDictionary<string, object> lockMap, string key, object lockInstance)
    {
        if (lockMap.TryGetValue(key, out var existing) && ReferenceEquals(existing, lockInstance))
        {
            lockMap.TryRemove(key, out _);
        }
    }
}
