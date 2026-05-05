using BzsOIDC.Idp.Infra;
using BzsOIDC.Idp.Models;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BzsOIDC.Idp.Services.Identity;

public interface IPermissionCatalogService
{
    Task<IReadOnlyList<ProtectedResourceResponse>> GetResourcesAsync(CancellationToken cancellationToken = default);
    Task<ProtectedResourceResponse?> GetResourceAsync(string resourceKey, CancellationToken cancellationToken = default);
    Task<PermissionDefinitionResponse?> GetPermissionAsync(string permissionName, CancellationToken cancellationToken = default);
    Task<PermissionCatalogCommandResult<ProtectedResourceResponse>> UpsertResourceAsync(
        string resourceKey,
        ProtectedResourceUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<PermissionCatalogCommandResult<PermissionDefinitionResponse>> UpsertPermissionAsync(
        string resourceKey,
        string permissionName,
        PermissionDefinitionUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<PermissionCatalogCommandResult<PermissionDefinitionResponse>> SyncReleaseScopesAsync(
        string permissionName,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string[]>> ResolveReleaseScopesAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);

    Task<string[]> ValidateAssignablePermissionsAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);

    Task InitializeDefaultsAsync(
        IEnumerable<PermissionCatalogSeedResource> resources,
        CancellationToken cancellationToken = default);
}

internal sealed class PermissionCatalogService(
    IdpDbContext dbContext,
    IServiceProvider? serviceProvider,
    IMemoryCache cache) : IPermissionCatalogService
{
    private const string ReleaseScopesCacheKey = "idp.permission-catalog.release-scopes";
    private static readonly string[] ObsoleteSeedResourceKeys = ["legacy-api", "oidc-clients", "oidc-scopes"];

    public async Task<IReadOnlyList<ProtectedResourceResponse>> GetResourcesAsync(CancellationToken cancellationToken = default)
    {
        var resources = await QueryResources()
            .OrderBy(static resource => resource.Key)
            .ToListAsync(cancellationToken);

        var roleAssignments = await GetAssignedRolesByPermissionAsync(cancellationToken);
        return resources.Select(resource => ToResponse(resource, roleAssignments)).ToArray();
    }

    public async Task<ProtectedResourceResponse?> GetResourceAsync(string resourceKey, CancellationToken cancellationToken = default)
    {
        var normalizedKey = ProtectedResource.NormalizeKey(resourceKey);
        var resource = await QueryResources()
            .FirstOrDefaultAsync(resource => resource.Key == normalizedKey, cancellationToken);

        if (resource is null)
        {
            return null;
        }

        var roleAssignments = await GetAssignedRolesByPermissionAsync(cancellationToken);
        return ToResponse(resource, roleAssignments);
    }

    public async Task<PermissionDefinitionResponse?> GetPermissionAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        var normalizedName = PermissionDefinition.NormalizeName(permissionName);
        var permission = await QueryPermissions()
            .FirstOrDefaultAsync(permission => permission.Name == normalizedName, cancellationToken);

        if (permission is null)
        {
            return null;
        }

        var roleAssignments = await GetAssignedRolesByPermissionAsync(cancellationToken);
        return ToResponse(permission, roleAssignments);
    }

    public async Task<PermissionCatalogCommandResult<ProtectedResourceResponse>> UpsertResourceAsync(
        string resourceKey,
        ProtectedResourceUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = ProtectedResource.NormalizeKey(resourceKey);
        var resource = await QueryResources()
            .FirstOrDefaultAsync(resource => resource.Key == normalizedKey, cancellationToken);

        if (resource is null)
        {
            resource = ProtectedResource.Create(normalizedKey, request.DisplayName, request.Description);
            resource.Update(request.DisplayName ?? normalizedKey, request.Description, request.IsActive);
            await dbContext.AddAsync(resource, cancellationToken);
        }
        else
        {
            resource.Update(request.DisplayName ?? normalizedKey, request.Description, request.IsActive);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return PermissionCatalogCommandResult<ProtectedResourceResponse>.Success(ToResponse(resource, EmptyRoleAssignments));
    }

    public async Task<PermissionCatalogCommandResult<PermissionDefinitionResponse>> UpsertPermissionAsync(
        string resourceKey,
        string permissionName,
        PermissionDefinitionUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedResourceKey = ProtectedResource.NormalizeKey(resourceKey);
        var normalizedPermissionName = PermissionDefinition.NormalizeName(permissionName);

        var resource = await QueryResources()
            .FirstOrDefaultAsync(resource => resource.Key == normalizedResourceKey, cancellationToken);

        if (resource is null)
        {
            return PermissionCatalogCommandResult<PermissionDefinitionResponse>.Failure(
                PermissionCatalogCommandStatus.NotFound,
                $"Resource/API '{normalizedResourceKey}' was not found.");
        }

        var permission = resource.Permissions
            .FirstOrDefault(permission => string.Equals(permission.Name, normalizedPermissionName, StringComparison.OrdinalIgnoreCase));

        if (permission is null)
        {
            permission = resource.AddPermission(normalizedPermissionName, request.DisplayName, request.Description);
        }
        else
        {
            permission.Update(request.DisplayName, request.Description, request.IsActive);
        }
        permission.Update(request.DisplayName, request.Description, request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ReleaseScopesCacheKey);

        return PermissionCatalogCommandResult<PermissionDefinitionResponse>.Success(ToResponse(permission, EmptyRoleAssignments));
    }

    public async Task<PermissionCatalogCommandResult<PermissionDefinitionResponse>> SyncReleaseScopesAsync(
        string permissionName,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermissionName = PermissionDefinition.NormalizeName(permissionName);
        var targetScopes = scopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(PermissionReleaseScope.NormalizeScope)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (targetScopes.Length == 0)
        {
            return PermissionCatalogCommandResult<PermissionDefinitionResponse>.Failure(
                PermissionCatalogCommandStatus.ValidationFailed,
                "At least one release scope is required.");
        }

        var permission = await QueryPermissions()
            .FirstOrDefaultAsync(permission => permission.Name == normalizedPermissionName, cancellationToken);

        if (permission is null)
        {
            return PermissionCatalogCommandResult<PermissionDefinitionResponse>.Failure(
                PermissionCatalogCommandStatus.NotFound,
                $"Permission '{normalizedPermissionName}' was not found.");
        }

        permission.SyncReleaseScopes(targetScopes);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ReleaseScopesCacheKey);

        var roleAssignments = await GetAssignedRolesByPermissionAsync(cancellationToken);
        return PermissionCatalogCommandResult<PermissionDefinitionResponse>.Success(ToResponse(permission, roleAssignments));
    }

    public async Task<IReadOnlyDictionary<string, string[]>> ResolveReleaseScopesAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermissions = permissions
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(PermissionDefinition.NormalizeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPermissions.Length == 0)
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        var mappings = await GetReleaseScopeMappingsAsync(cancellationToken);
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

    public async Task<string[]> ValidateAssignablePermissionsAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        var targetPermissions = permissions
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(PermissionDefinition.NormalizeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (targetPermissions.Length == 0)
        {
            return [];
        }

        var activePermissions = await dbContext.PermissionDefinitions
            .AsNoTracking()
            .Where(static permission => permission.IsActive && permission.Resource.IsActive)
            .Select(static permission => permission.Name)
            .ToListAsync(cancellationToken);

        var activeSet = activePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return targetPermissions
            .Where(permission => !activeSet.Contains(permission))
            .OrderBy(static permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task InitializeDefaultsAsync(IEnumerable<PermissionCatalogSeedResource> resources, CancellationToken cancellationToken = default)
    {
        await RemoveObsoleteSeedResourcesAsync(cancellationToken);

        var resourcesByKey = await QueryResources()
            .ToDictionaryAsync(static resource => resource.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var permissionsByName = resourcesByKey.Values
            .SelectMany(static resource => resource.Permissions)
            .ToDictionary(static permission => permission.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var seedResource in resources
                     .Where(static resource => !string.IsNullOrWhiteSpace(resource.ResourceKey))
                     .GroupBy(static resource => ProtectedResource.NormalizeKey(resource.ResourceKey), StringComparer.OrdinalIgnoreCase)
                     .Select(static group => group.Last()))
        {
            var resourceKey = ProtectedResource.NormalizeKey(seedResource.ResourceKey);
            if (!resourcesByKey.TryGetValue(resourceKey, out var resource))
            {
                resource = GetTrackedResource(resourceKey)
                    ?? ProtectedResource.Create(resourceKey, seedResource.DisplayName, seedResource.Description);

                if (dbContext.Entry(resource).State == EntityState.Detached)
                {
                    await dbContext.AddAsync(resource, cancellationToken);
                }

                resourcesByKey[resourceKey] = resource;
            }

            resource.Update(seedResource.DisplayName ?? resource.DisplayName, seedResource.Description, isActive: true);

            foreach (var seedPermission in seedResource.Permissions.Where(static permission => !string.IsNullOrWhiteSpace(permission.Name)))
            {
                var permissionName = PermissionDefinition.NormalizeName(seedPermission.Name);
                var permission = resource.Permissions
                    .FirstOrDefault(permission => string.Equals(permission.Name, permissionName, StringComparison.OrdinalIgnoreCase));

                if (permission is null)
                {
                    permission = GetTrackedPermission(resource, permissionName);
                    if (permission is null)
                    {
                        if (permissionsByName.ContainsKey(permissionName))
                        {
                            continue;
                        }

                        permission = resource.AddPermission(permissionName, seedPermission.DisplayName, seedPermission.Description);
                        permissionsByName[permissionName] = permission;
                    }
                }

                var releaseScopes = seedPermission.ReleaseScopes.Length > 0
                    ? seedPermission.ReleaseScopes
                    : [resourceKey];
                permission.SyncReleaseScopes(releaseScopes);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ReleaseScopesCacheKey);
    }

    private async Task RemoveObsoleteSeedResourcesAsync(CancellationToken cancellationToken)
    {
        var obsoleteResources = await QueryResources()
            .Where(resource => ObsoleteSeedResourceKeys.Contains(resource.Key))
            .ToArrayAsync(cancellationToken);

        if (obsoleteResources.Length > 0)
        {
            dbContext.RemoveRange(obsoleteResources);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private ProtectedResource? GetTrackedResource(string resourceKey)
    {
        return dbContext.ChangeTracker
            .Entries<ProtectedResource>()
            .Select(static entry => entry.Entity)
            .FirstOrDefault(resource => string.Equals(resource.Key, resourceKey, StringComparison.OrdinalIgnoreCase));
    }

    private static PermissionDefinition? GetTrackedPermission(ProtectedResource resource, string permissionName)
    {
        return resource.Permissions
            .FirstOrDefault(permission => string.Equals(permission.Name, permissionName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyDictionary<string, string[]>> GetReleaseScopeMappingsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(ReleaseScopesCacheKey, out IReadOnlyDictionary<string, string[]>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var records = await dbContext.PermissionDefinitions
            .AsNoTracking()
            .Where(static permission => permission.IsActive && permission.Resource.IsActive)
            .Select(static permission => new
            {
                permission.Name,
                Scopes = permission.ReleaseScopes.Select(static scope => scope.Scope).ToArray(),
            })
            .ToListAsync(cancellationToken);

        var mappings = records
            .Where(static record => record.Scopes.Length > 0)
            .ToDictionary(
                static record => record.Name,
                static record => record.Scopes
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static scope => scope, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        cache.Set(ReleaseScopesCacheKey, mappings, TimeSpan.FromMinutes(5));
        return mappings;
    }

    private async Task<IReadOnlyDictionary<string, RolePermissionAssignmentResponse[]>> GetAssignedRolesByPermissionAsync(
        CancellationToken cancellationToken)
    {
        if (serviceProvider is null)
        {
            return EmptyRoleAssignments;
        }

        var roleManager = serviceProvider.GetService<RoleManager<BzsRole>>();
        if (roleManager is null)
        {
            return EmptyRoleAssignments;
        }

        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(static role => role.Name)
            .ToListAsync(cancellationToken);

        var assignments = new Dictionary<string, List<RolePermissionAssignmentResponse>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            var claims = await roleManager.GetClaimsAsync(role);
            foreach (var permission in claims
                         .Where(static claim => string.Equals(claim.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase))
                         .Select(static claim => PermissionDefinition.NormalizeName(claim.Value))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!assignments.TryGetValue(permission, out var roleAssignments))
                {
                    roleAssignments = [];
                    assignments[permission] = roleAssignments;
                }

                roleAssignments.Add(new RolePermissionAssignmentResponse
                {
                    RoleId = role.Id,
                    RoleName = role.Name ?? role.Id.ToString(),
                    Assigned = true,
                });
            }
        }

        return assignments.ToDictionary(
            static item => item.Key,
            static item => item.Value.OrderBy(static role => role.RoleName, StringComparer.OrdinalIgnoreCase).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private IQueryable<ProtectedResource> QueryResources()
    {
        return dbContext.ProtectedResources
            .Include(static resource => resource.Permissions)
            .ThenInclude(static permission => permission.ReleaseScopes);
    }

    private static IReadOnlyDictionary<string, RolePermissionAssignmentResponse[]> EmptyRoleAssignments { get; } =
        new Dictionary<string, RolePermissionAssignmentResponse[]>(StringComparer.OrdinalIgnoreCase);

    private IQueryable<PermissionDefinition> QueryPermissions()
    {
        return dbContext.PermissionDefinitions
            .Include(static permission => permission.Resource)
            .Include(static permission => permission.ReleaseScopes);
    }

    private static ProtectedResourceResponse ToResponse(
        ProtectedResource resource,
        IReadOnlyDictionary<string, RolePermissionAssignmentResponse[]> roleAssignments)
    {
        return new ProtectedResourceResponse
        {
            Key = resource.Key,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            IsActive = resource.IsActive,
            Permissions = resource.Permissions
                .OrderBy(static permission => permission.Name, StringComparer.OrdinalIgnoreCase)
                .Select(permission => ToResponse(permission, roleAssignments))
                .ToArray(),
        };
    }

    private static PermissionDefinitionResponse ToResponse(
        PermissionDefinition permission,
        IReadOnlyDictionary<string, RolePermissionAssignmentResponse[]> roleAssignments)
    {
        return new PermissionDefinitionResponse
        {
            ResourceKey = permission.Resource.Key,
            Name = permission.Name,
            DisplayName = permission.DisplayName,
            Description = permission.Description,
            IsActive = permission.IsActive,
            ReleaseScopes = permission.ReleaseScopes
                .Select(static scope => scope.Scope)
                .OrderBy(static scope => scope, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AssignedRoles = roleAssignments.TryGetValue(permission.Name, out var roles) ? roles : [],
        };
    }
}
