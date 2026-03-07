using System.Security.Claims;
using BzsCenter.Idp.Domain;
using BzsCenter.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BzsCenter.Idp.Services.Identity;

public interface IRolePermissionService
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<IdentityResult> AddPermissionAsync(Guid roleId, string permission, CancellationToken cancellationToken = default);
    Task<IdentityResult> RemovePermissionAsync(Guid roleId, string permission, CancellationToken cancellationToken = default);
    Task<IdentityResult> SyncPermissionsAsync(Guid roleId, IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);
}

internal sealed class RolePermissionService(RoleManager<BzsRole> roleManager) : IRolePermissionService
{
    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return [];
        }

        var claims = await roleManager.GetClaimsAsync(role);
        return claims
            .Where(static c => string.Equals(c.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(static c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static c => c)
            .ToArray();
    }

    public async Task<IdentityResult> AddPermissionAsync(Guid roleId, string permission,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return IdentityResult.Failed(CreateError("RoleNotFound", $"角色 '{roleId}' 不存在"));
        }

        var normalizedPermission = permission.Trim();
        var claims = await roleManager.GetClaimsAsync(role);
        var exists = claims.Any(c =>
            string.Equals(c.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.Value, normalizedPermission, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            return IdentityResult.Success;
        }

        return await roleManager.AddClaimAsync(role, new Claim(PermissionConstants.ClaimType, normalizedPermission));
    }

    public async Task<IdentityResult> RemovePermissionAsync(Guid roleId, string permission,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return IdentityResult.Failed(CreateError("RoleNotFound", $"角色 '{roleId}' 不存在"));
        }

        var normalizedPermission = permission.Trim();
        var claims = await roleManager.GetClaimsAsync(role);
        var targetClaims = claims.Where(c =>
                string.Equals(c.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Value, normalizedPermission, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var claim in targetClaims)
        {
            var result = await roleManager.RemoveClaimAsync(role, claim);
            if (!result.Succeeded)
            {
                return result;
            }
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SyncPermissionsAsync(Guid roleId, IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return IdentityResult.Failed(CreateError("RoleNotFound", $"角色 '{roleId}' 不存在"));
        }

        var targetSet = permissions
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingClaims = await roleManager.GetClaimsAsync(role);
        var existingSet = existingClaims
            .Where(static c => string.Equals(c.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(static c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in targetSet.Except(existingSet, StringComparer.OrdinalIgnoreCase))
        {
            var addResult = await roleManager.AddClaimAsync(role, new Claim(PermissionConstants.ClaimType, permission));
            if (!addResult.Succeeded)
            {
                return addResult;
            }
        }

        foreach (var permission in existingSet.Except(targetSet, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var claim in existingClaims.Where(c =>
                         string.Equals(c.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)))
            {
                var removeResult = await roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    return removeResult;
                }
            }
        }

        return IdentityResult.Success;
    }

    private static IdentityError CreateError(string code, string description)
    {
        return new IdentityError
        {
            Code = code,
            Description = description,
        };
    }
}
