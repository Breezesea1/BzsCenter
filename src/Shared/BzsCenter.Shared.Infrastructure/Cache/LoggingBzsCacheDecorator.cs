using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using BzsCenter.Shared.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace BzsCenter.Shared.Infrastructure.Cache;

internal sealed class LoggingBzsCacheDecorator(
    IBzsCache inner,
    ILogger<LoggingBzsCacheDecorator> logger,
    CacheOptions cacheOptions) : IBzsCache
{
    internal const string ActivitySourceName = "BzsCenter.Cache";
    internal const string MeterName = "BzsCenter.Cache";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> OperationCounter = Meter.CreateCounter<long>("cache.operation.count");
    private static readonly Counter<long> OperationErrorCounter = Meter.CreateCounter<long>("cache.operation.error.count");
    private static readonly Counter<long> HitCounter = Meter.CreateCounter<long>("cache.hit.count");
    private static readonly Counter<long> MissCounter = Meter.CreateCounter<long>("cache.miss.count");
    private static readonly UpDownCounter<long> InflightCounter = Meter.CreateUpDownCounter<long>("cache.operation.inflight");
    private static readonly Counter<long> FactoryCounter = Meter.CreateCounter<long>("cache.factory.execution.count");
    private static readonly Histogram<double> OperationDurationMs = Meter.CreateHistogram<double>("cache.operation.duration", "ms");
    private static readonly Histogram<double> FactoryDurationMs = Meter.CreateHistogram<double>("cache.factory.execution.duration", "ms");

    private readonly string _cacheSystem = cacheOptions.CacheType == CacheType.Redis ? "redis" : "inmemory";

    // 包装写入操作并记录日志与遥测。
    public void Set<T>(string key, T? value, EntryCacheOptions<T> options, IReadOnlyCollection<string>? tags = null)
    {
        Execute("set", key, () =>
            {
                inner.Set(key, value, options, tags);
                return true;
            },
            static _ => null);
    }

    // 包装异步写入操作并记录日志与遥测。
    public Task SetAsync<T>(string key, T? value, EntryCacheOptions<T> options, IReadOnlyCollection<string>? tags = null)
    {
        return ExecuteAsync("set", key, async () =>
            {
                await inner.SetAsync(key, value, options, tags);
                return true;
            },
            static _ => null);
    }

    // 包装删除操作并记录日志与遥测。
    public void Remove(string key)
    {
        Execute("remove", key, () =>
            {
                inner.Remove(key);
                return true;
            },
            static _ => null);
    }

    // 包装异步删除操作并记录日志与遥测。
    public Task RemoveAsync(string key)
    {
        return ExecuteAsync("remove", key, async () =>
            {
                await inner.RemoveAsync(key);
                return true;
            },
            static _ => null);
    }

    // 包装读取操作并上报命中状态。
    public T? Get<T>(string key, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        var value = Execute("get", key, () => inner.Get(key, jsonTypeInfo), static cacheValue => cacheValue is not null);

        return value;
    }

    // 包装异步读取操作并上报命中状态。
    public async Task<T?> GetAsync<T>(string key, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        var value = await ExecuteAsync("get", key, () => inner.GetAsync(key, jsonTypeInfo), static cacheValue => cacheValue is not null);

        return value;
    }

    // 包装 TryGet 操作并上报命中状态。
    public bool TryGet<T>(string key, out T? value, JsonTypeInfo<T?>? jsonTypeInfo)
    {
        T? cacheValue = default;
        var hit = Execute("try_get", key, () =>
            {
                var ok = inner.TryGet(key, out var v, jsonTypeInfo);
                cacheValue = v;
                return ok;
            },
            static ok => ok);

        value = cacheValue;
        return hit;
    }

    // 包装递增操作并记录耗时。
    public Task<long> IncrementAsync(string key, long delta = 1, TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        return ExecuteAsync("increment", key, () => inner.IncrementAsync(key, delta, absoluteExpirationRelativeToNow), static _ => null);
    }

    // 包装 GetOrCreate 操作并记录耗时。
    public Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        EntryCacheOptions<T> options,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        async Task<T> WrappedFactory(CancellationToken ct)
        {
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                var value = await factory(ct);
                RecordFactorySuccess(startedAt);
                return value;
            }
            catch (Exception)
            {
                RecordFactoryFailure(startedAt);
                throw;
            }
        }

        return ExecuteAsync("get_or_create", key,
            () => inner.GetOrCreateAsync(key, WrappedFactory, options, tags, cancellationToken),
            static _ => null);
    }

    // 包装按标签删除操作并记录耗时。
    public Task RemoveByTagAsync(string tag)
    {
        return ExecuteAsync("remove_by_tag", tag, async () =>
            {
                await inner.RemoveByTagAsync(tag);
                return true;
            },
            static _ => null);
    }

    // 同步执行模板：统一处理 Activity、指标与异常。
    private T Execute<T>(string operation, string key, Func<T> callback, Func<T, bool?> hitSelector)
    {
        using var activity = CreateActivity(operation, key);
        var startedAt = Stopwatch.GetTimestamp();
        InflightCounter.Add(1,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("operation", operation));

        try
        {
            var result = callback();
            var isHit = hitSelector(result);
            RecordSuccess(operation, startedAt, isHit, key, activity);
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(operation, startedAt, key, ex, activity);
            throw;
        }
        finally
        {
            InflightCounter.Add(-1,
                new KeyValuePair<string, object?>("cache.system", _cacheSystem),
                new KeyValuePair<string, object?>("operation", operation));
        }
    }

    // 异步执行模板：统一处理 Activity、指标与异常。
    private async Task<T> ExecuteAsync<T>(string operation, string key, Func<Task<T>> callback, Func<T, bool?> hitSelector)
    {
        using var activity = CreateActivity(operation, key);
        var startedAt = Stopwatch.GetTimestamp();
        InflightCounter.Add(1,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("operation", operation));

        try
        {
            var result = await callback();
            var isHit = hitSelector(result);
            RecordSuccess(operation, startedAt, isHit, key, activity);
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(operation, startedAt, key, ex, activity);
            throw;
        }
        finally
        {
            InflightCounter.Add(-1,
                new KeyValuePair<string, object?>("cache.system", _cacheSystem),
                new KeyValuePair<string, object?>("operation", operation));
        }
    }

    // 创建一次缓存操作的 Trace Activity。
    private Activity? CreateActivity(string operation, string key)
    {
        var activity = ActivitySource.StartActivity($"{operation} {_cacheSystem}", ActivityKind.Client);
        activity?.SetTag("db.system", _cacheSystem);
        activity?.SetTag("db.operation", operation);
        activity?.SetTag("cache.key_hash", HashKey(key));
        return activity;
    }

    // 记录成功指标与调试日志。
    private void RecordSuccess(string operation, long startedAt, bool? isHit, string key, Activity? activity)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var outcome = "success";

        if (isHit.HasValue)
        {
            activity?.SetTag("cache.hit", isHit.Value);
            outcome = isHit.Value ? "hit" : "miss";
            if (isHit.Value)
            {
                HitCounter.Add(1,
                    new KeyValuePair<string, object?>("cache.system", _cacheSystem),
                    new KeyValuePair<string, object?>("operation", operation));
            }
            else
            {
                MissCounter.Add(1,
                    new KeyValuePair<string, object?>("cache.system", _cacheSystem),
                    new KeyValuePair<string, object?>("operation", operation));
            }
        }

        OperationCounter.Add(1,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", "ok"),
            new KeyValuePair<string, object?>("outcome", outcome));

        OperationDurationMs.Record(elapsed,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", "ok"),
            new KeyValuePair<string, object?>("outcome", outcome));

        logger.LogDebug("Cache operation {CacheOperation} succeeded in {ElapsedMs}ms for key hash {CacheKeyHash}.",
            operation, elapsed, HashKey(key));
    }

    // 记录失败指标与错误日志。
    private void RecordFailure(string operation, long startedAt, string key, Exception ex, Activity? activity)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        activity?.SetExceptionTags(ex);
        OperationCounter.Add(1,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", "error"),
            new KeyValuePair<string, object?>("outcome", "error"));
        OperationErrorCounter.Add(1,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("operation", operation));

        OperationDurationMs.Record(elapsed,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", "error"),
            new KeyValuePair<string, object?>("outcome", "error"));

        logger.LogError(ex,
            "Cache operation {CacheOperation} failed in {ElapsedMs}ms for key hash {CacheKeyHash}.",
            operation, elapsed, HashKey(key));
    }

    // 对缓存键做哈希，避免日志/追踪暴露原始 key。
    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash);
    }

    // 记录 factory 成功执行次数与耗时。
    private void RecordFactorySuccess(long startedAt)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        FactoryCounter.Add(1,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("outcome", "success"));
        FactoryDurationMs.Record(elapsed,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("outcome", "success"));
    }

    // 记录 factory 失败执行次数与耗时。
    private void RecordFactoryFailure(long startedAt)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        FactoryCounter.Add(1,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("outcome", "error"));
        FactoryDurationMs.Record(elapsed,
            new KeyValuePair<string, object?>("cache.system", _cacheSystem),
            new KeyValuePair<string, object?>("outcome", "error"));
    }
}
