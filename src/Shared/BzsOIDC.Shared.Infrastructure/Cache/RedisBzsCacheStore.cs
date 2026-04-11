using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BzsOIDC.Shared.Infrastructure.Cache;

internal sealed class RedisBzsCacheStore(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<CacheOptions> cacheOptions) : IBzsCacheStore
{
    private readonly CacheOptions _cacheOptions = cacheOptions.Value;
    private readonly IDatabase _db = connectionMultiplexer.GetDatabase();
    private const string DataPrefix = "data:";
    private const string CounterPrefix = "counter:";
    private const string TagPrefix = "tag:";
    private const string KeyTagsPrefix = "key-tags:";

    // 同步写入 Redis 数据键并更新标签索引。
    public void Set(string key, byte[] value, EntryCacheTimeOptions options, IReadOnlyCollection<string>? tags = null)
    {
        if (options.SlidingExpiration.HasValue)
        {
            // 当前 Redis 实现只支持绝对过期，避免静默忽略配置造成误解。
            throw new NotSupportedException("Redis store does not support sliding expiration in this implementation.");
        }

        var dataKey = DataKey(key);
        _db.StringSet(dataKey, value, options.AbsoluteExpirationRelativeToNow, keepTtl: false);
        ReplaceTags(dataKey, tags, options.AbsoluteExpirationRelativeToNow);
    }

    // 异步写入 Redis 数据键并更新标签索引。
    public async Task SetAsync(string key, byte[] value, EntryCacheTimeOptions options, IReadOnlyCollection<string>? tags = null)
    {
        if (options.SlidingExpiration.HasValue)
        {
            throw new NotSupportedException("Redis store does not support sliding expiration in this implementation.");
        }

        var dataKey = DataKey(key);
        await _db.StringSetAsync(dataKey, value, options.AbsoluteExpirationRelativeToNow, keepTtl: false);
        await ReplaceTagsAsync(dataKey, tags, options.AbsoluteExpirationRelativeToNow);
    }

    // 同步删除 Redis 数据键及其标签索引。
    public void Remove(string key)
    {
        var dataKey = DataKey(key);
        _db.KeyDelete(dataKey);
        RemoveKeyTagIndex(dataKey);
    }

    // 异步删除 Redis 数据键及其标签索引。
    public async Task RemoveAsync(string key)
    {
        var dataKey = DataKey(key);
        await _db.KeyDeleteAsync(dataKey);
        await RemoveKeyTagIndexAsync(dataKey);
    }

    // 同步读取 Redis 数据键。
    public byte[]? Get(string key)
    {
        var value = _db.StringGet(DataKey(key));
        return value.HasValue ? (byte[]?)value : null;
    }

    // 异步读取 Redis 数据键。
    public Task<byte[]?> GetAsync(string key)
    {
        return GetAsyncCore(key);
    }

    // 尝试读取 Redis 数据键并返回命中状态。
    public bool TryGet(string key, out byte[]? value)
    {
        var redisValue = _db.StringGet(DataKey(key));
        value = redisValue.HasValue ? (byte[]?)redisValue : null;
        return value is not null;
    }

    // 对 Redis 计数器键执行原子递增。
    public async Task<long> IncrementAsync(string key, long delta = 1, TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        var counterKey = CounterKey(key);

        var next = await _db.StringIncrementAsync(counterKey, delta);
        if (absoluteExpirationRelativeToNow.HasValue)
        {
            await _db.KeyExpireAsync(counterKey, absoluteExpirationRelativeToNow.Value);
        }

        return next;
    }

    // 按标签删除数据键，并清理索引脏成员。
    public async Task RemoveByTagAsync(string tag)
    {
        var tagKey = TagIndexKey(tag);
        var keys = await _db.SetMembersAsync(tagKey);

        if (keys.Length == 0)
        {
            return;
        }

        var tasks = new List<Task>(keys.Length * 2 + 1);
        foreach (var redisValue in keys)
        {
            var dataKey = redisValue.ToString();
            if (string.IsNullOrWhiteSpace(dataKey))
            {
                continue;
            }

            // 二次确认成员关系，降低并发改写期间误删新值的概率。
            var stillTagged = await _db.SetContainsAsync(tagKey, dataKey);
            if (!stillTagged)
            {
                continue;
            }

            var exists = await _db.KeyExistsAsync(dataKey);
            if (!exists)
            {
                tasks.Add(_db.SetRemoveAsync(tagKey, dataKey));
                continue;
            }

            tasks.Add(_db.KeyDeleteAsync(dataKey));
            tasks.Add(RemoveKeyTagIndexAsync(dataKey));
        }

        tasks.Add(_db.KeyDeleteAsync(tagKey));
        await Task.WhenAll(tasks);
    }

    // 同步替换 key 的标签索引关系。
    private void ReplaceTags(string dataKey, IReadOnlyCollection<string>? tags,
        TimeSpan absoluteExpiration)
    {
        RemoveKeyTagIndex(dataKey);

        if (tags is null || tags.Count == 0)
        {
            return;
        }

        var validTags = tags.Where(static t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).ToArray();
        if (validTags.Length == 0)
        {
            return;
        }

        var keyTagsKey = KeyTagIndexKey(dataKey);
        var batch = _db.CreateBatch();
        foreach (var tag in validTags)
        {
            _ = batch.SetAddAsync(TagIndexKey(tag), dataKey);
            _ = batch.SetAddAsync(keyTagsKey, tag);
        }

        _ = batch.KeyExpireAsync(keyTagsKey, absoluteExpiration);
        batch.Execute();
    }

    // 异步替换 key 的标签索引关系。
    private async Task ReplaceTagsAsync(string dataKey, IReadOnlyCollection<string>? tags,
        TimeSpan absoluteExpiration)
    {
        await RemoveKeyTagIndexAsync(dataKey);

        if (tags is null || tags.Count == 0)
        {
            return;
        }

        var validTags = tags.Where(static t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).ToArray();
        if (validTags.Length == 0)
        {
            return;
        }

        var keyTagsKey = KeyTagIndexKey(dataKey);
        var tasks = new List<Task>(validTags.Length * 2 + 1);
        foreach (var tag in validTags)
        {
            tasks.Add(_db.SetAddAsync(TagIndexKey(tag), dataKey));
            tasks.Add(_db.SetAddAsync(keyTagsKey, tag));
        }

        tasks.Add(_db.KeyExpireAsync(keyTagsKey, absoluteExpiration));

        await Task.WhenAll(tasks);
    }

    // 同步清理 key 与标签的双向索引。
    private void RemoveKeyTagIndex(string dataKey)
    {
        var keyTagsKey = KeyTagIndexKey(dataKey);
        var tags = _db.SetMembers(keyTagsKey);
        if (tags.Length == 0)
        {
            _db.KeyDelete(keyTagsKey);
            return;
        }

        var batch = _db.CreateBatch();
        foreach (var redisValue in tags)
        {
            var tag = redisValue.ToString();
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            _ = batch.SetRemoveAsync(TagIndexKey(tag), dataKey);
        }

        _ = batch.KeyDeleteAsync(keyTagsKey);
        batch.Execute();
    }

    // 异步清理 key 与标签的双向索引。
    private async Task RemoveKeyTagIndexAsync(string dataKey)
    {
        var keyTagsKey = KeyTagIndexKey(dataKey);
        var tags = await _db.SetMembersAsync(keyTagsKey);
        if (tags.Length == 0)
        {
            await _db.KeyDeleteAsync(keyTagsKey);
            return;
        }

        var tasks = new List<Task>(tags.Length + 1);
        foreach (var redisValue in tags)
        {
            var tag = redisValue.ToString();
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            tasks.Add(_db.SetRemoveAsync(TagIndexKey(tag), dataKey));
        }

        tasks.Add(_db.KeyDeleteAsync(keyTagsKey));
        await Task.WhenAll(tasks);
    }

    // 异步读取核心逻辑。
    private async Task<byte[]?> GetAsyncCore(string key)
    {
        var redisValue = await _db.StringGetAsync(DataKey(key));
        return redisValue.HasValue ? (byte[]?)redisValue : null;
    }

    // 生成数据键。
    private string DataKey(string key)
    {
        return BuildKey(DataPrefix, key);
    }

    // 生成计数器键。
    private string CounterKey(string key)
    {
        return BuildKey(CounterPrefix, key);
    }

    // 生成标签索引键。
    private string TagIndexKey(string tag)
    {
        return BuildKey(TagPrefix, tag);
    }

    // 生成 key->tags 索引键。
    private string KeyTagIndexKey(string dataKey)
    {
        return BuildKey(KeyTagsPrefix, dataKey);
    }

    // 按统一规则拼装 Redis 物理键。
    private string BuildKey(string category, string key)
    {
        return $"{_cacheOptions.RedisInstanceName}{_cacheOptions.KeyPrefix}{category}{key}";
    }
}
