using BzsOIDC.Idp.Models;
using BzsOIDC.Idp.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BzsOIDC.Idp.Services.Identity;

public interface IPermissionScopeService
{
    Task<IReadOnlyList<PermissionScopeResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PermissionScopeResponse?> GetByPermissionAsync(string permission, CancellationToken cancellationToken = default);
    Task UpsertAsync(string permission, IEnumerable<string> scopes, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string permission, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string[]>> ResolveScopesAsync(IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);

    Task InitializeDefaultsIfEmptyAsync(IReadOnlyDictionary<string, string[]> defaults,
        CancellationToken cancellationToken = default);
}

internal sealed class PermissionScopeService(IdpDbContext dbContext, IMemoryCache cache) : IPermissionScopeService
{
    private const string PermissionScopeCacheKey = "idp.permission-scope.mappings";

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IReadOnlyList<PermissionScopeResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var mappings = await GetMappingsAsync(cancellationToken);
        return mappings
            .Select(static kv => new PermissionScopeResponse
            {
                Permission = kv.Key,
                Scopes = kv.Value.ToArray(),
            })
            .OrderBy(static x => x.Permission, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<PermissionScopeResponse?> GetByPermissionAsync(string permission,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermission = NormalizePermission(permission);
        var mappings = await GetMappingsAsync(cancellationToken);
        if (!mappings.TryGetValue(normalizedPermission, out var scopes))
        {
            return null;
        }

        return new PermissionScopeResponse
        {
            Permission = normalizedPermission,
            Scopes = scopes.ToArray(),
        };
    }

    /// <summary>
    /// 执行UpsertAsync。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <param name="scopes">参数scopes。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task UpsertAsync(string permission, IEnumerable<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermission = NormalizePermission(permission);
        var targetScopes = NormalizeScopes(scopes);

        if (targetScopes.Length == 0)
        {
            throw new ArgumentException("At least one scope is required.", nameof(scopes));
        }

        var existing = await dbContext.Set<PermissionScopeMapping>()
            .Where(x => x.Permission == normalizedPermission)
            .ToListAsync(cancellationToken);

        var existingScopes = existing
            .Select(static x => x.Scope)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toRemove = existing
            .Where(x => !targetScopes.Contains(x.Scope, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (toRemove.Length > 0)
        {
            dbContext.RemoveRange(toRemove);
        }

        var toAdd = targetScopes
            .Where(scope => !existingScopes.Contains(scope))
            .Select(scope => PermissionScopeMapping.Create(normalizedPermission, scope))
            .ToArray();

        if (toAdd.Length > 0)
        {
            await dbContext.AddRangeAsync(toAdd, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(PermissionScopeCacheKey);
    }

    /// <summary>
    /// 删除数据。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<bool> DeleteAsync(string permission, CancellationToken cancellationToken = default)
    {
        var normalizedPermission = NormalizePermission(permission);
        var existing = await dbContext.Set<PermissionScopeMapping>()
            .Where(x => x.Permission == normalizedPermission)
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
        {
            return false;
        }

        dbContext.RemoveRange(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(PermissionScopeCacheKey);
        return true;
    }

    /// <summary>
    /// 解析并返回结果。
    /// </summary>
    /// <param name="permissions">参数permissions。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IReadOnlyDictionary<string, string[]>> ResolveScopesAsync(IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermissions = permissions
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPermissions.Length == 0)
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        var mappings = await GetMappingsAsync(cancellationToken);
        var resolved = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in normalizedPermissions)
        {
            if (mappings.TryGetValue(permission, out var scopes))
            {
                resolved[permission] = scopes.ToArray();
            }
        }

        return resolved;
    }

    /// <summary>
    /// 执行InitializeDefaultsIfEmptyAsync。
    /// </summary>
    /// <param name="defaults">参数defaults。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task InitializeDefaultsIfEmptyAsync(IReadOnlyDictionary<string, string[]> defaults,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.Set<PermissionScopeMapping>().AnyAsync(cancellationToken))
        {
            return;
        }

        var seedEntries = defaults
            .Where(static kv => !string.IsNullOrWhiteSpace(kv.Key))
            .SelectMany(static kv => NormalizeScopes(kv.Value)
                .Select(scope => PermissionScopeMapping.Create(kv.Key, scope)))
            .DistinctBy(static x => new { x.Permission, x.Scope })
            .ToArray();

        if (seedEntries.Length == 0)
        {
            return;
        }

        await dbContext.AddRangeAsync(seedEntries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(PermissionScopeCacheKey);
    }


    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    private async Task<IReadOnlyDictionary<string, string[]>> GetMappingsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(PermissionScopeCacheKey, out IReadOnlyDictionary<string, string[]>? cachedMappings) &&
            cachedMappings is not null)
        {
            return cachedMappings;
        }

        var records = await dbContext.Set<PermissionScopeMapping>()
            .AsNoTracking()
            .OrderBy(static x => x.Permission)
            .ThenBy(static x => x.Scope)
            .ToListAsync(cancellationToken);

        var mappings = records
            .GroupBy(static x => x.Permission, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static g => g.Key,
                static g => g.Select(static x => x.Scope)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        cache.Set(PermissionScopeCacheKey, mappings, TimeSpan.FromMinutes(5));
        return mappings;
    }

    /// <summary>
    /// 执行NormalizePermission。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <returns>执行结果。</returns>
    private static string NormalizePermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return permission.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// 执行NormalizeScopes。
    /// </summary>
    /// <param name="scopes">参数scopes。</param>
    /// <returns>执行结果。</returns>
    private static string[] NormalizeScopes(IEnumerable<string> scopes)
    {
        return scopes
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Select(static s => s.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
