using System.Security.Claims;
using BzsOIDC.Idp.Models;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BzsOIDC.Idp.Services.Identity;

public interface IRoleManagementService
{
    Task<IReadOnlyList<RoleResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RoleResponse?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>?> GetPermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleManagementCommandResult<RoleResponse>> CreateAsync(RoleUpsertRequest request, CancellationToken cancellationToken = default);
    Task<RoleManagementCommandResult<RoleResponse>> UpdateAsync(Guid roleId, RoleUpsertRequest request, CancellationToken cancellationToken = default);
    Task<RoleManagementCommandResult<RoleResponse>> DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleManagementCommandResult<IReadOnlyList<string>>> SyncPermissionsAsync(
        Guid roleId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);
}

internal sealed class RoleManagementService(
    RoleManager<BzsRole> roleManager,
    IPermissionCatalogService permissionCatalogService,
    RoleManagementPolicy policy) : IRoleManagementService
{
    public async Task<IReadOnlyList<RoleResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(static role => role.Name)
            .ToListAsync(cancellationToken);

        var responses = new List<RoleResponse>(roles.Count);
        foreach (var role in roles)
        {
            responses.Add(await ToResponseAsync(role, includePermissions: false));
        }

        return responses;
    }

    public async Task<RoleResponse?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        return role is null ? null : await ToResponseAsync(role, includePermissions: true);
    }

    public async Task<IReadOnlyList<string>?> GetPermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        return role is null ? null : await GetPermissionClaimsAsync(role);
    }

    public async Task<RoleManagementCommandResult<RoleResponse>> CreateAsync(
        RoleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var reservedRoleExists = await roleManager.FindByNameAsync(IdentitySeedConstants.AdminRoleName) is not null;
        var validationErrors = policy.ValidateCreate(request.Name, reservedRoleExists);
        if (validationErrors.Length > 0)
        {
            return RoleManagementCommandResult<RoleResponse>.Failure(RoleManagementCommandStatus.ValidationFailed, validationErrors);
        }

        var normalizedName = policy.NormalizeName(request.Name);
        var existing = await roleManager.FindByNameAsync(normalizedName);
        if (existing is not null)
        {
            return RoleManagementCommandResult<RoleResponse>.Failure(
                RoleManagementCommandStatus.Conflict,
                $"角色 '{normalizedName}' 已存在");
        }

        var role = new BzsRole
        {
            Name = normalizedName,
            NormalizedName = policy.NormalizeKey(normalizedName),
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return FromIdentityFailure<RoleResponse>(RoleManagementCommandStatus.ValidationFailed, result);
        }

        return RoleManagementCommandResult<RoleResponse>.Success(await ToResponseAsync(role, includePermissions: true));
    }

    public async Task<RoleManagementCommandResult<RoleResponse>> UpdateAsync(
        Guid roleId,
        RoleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return RoleManagementCommandResult<RoleResponse>.Failure(
                RoleManagementCommandStatus.NotFound,
                $"角色 '{roleId}' 不存在");
        }

        var validationErrors = policy.ValidateRename(role, request.Name);
        if (validationErrors.Length > 0)
        {
            return RoleManagementCommandResult<RoleResponse>.Failure(
                policy.IsProtectedRole(role) ? RoleManagementCommandStatus.Protected : RoleManagementCommandStatus.ValidationFailed,
                validationErrors);
        }

        var normalizedName = policy.NormalizeName(request.Name);
        var existing = await roleManager.FindByNameAsync(normalizedName);
        if (existing is not null && existing.Id != role.Id)
        {
            return RoleManagementCommandResult<RoleResponse>.Failure(
                RoleManagementCommandStatus.Conflict,
                $"角色 '{normalizedName}' 已存在");
        }

        role.Name = normalizedName;
        role.NormalizedName = policy.NormalizeKey(normalizedName);

        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            return FromIdentityFailure<RoleResponse>(RoleManagementCommandStatus.ValidationFailed, result);
        }

        return RoleManagementCommandResult<RoleResponse>.Success(await ToResponseAsync(role, includePermissions: true));
    }

    public async Task<RoleManagementCommandResult<RoleResponse>> DeleteAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return RoleManagementCommandResult<RoleResponse>.Failure(
                RoleManagementCommandStatus.NotFound,
                $"角色 '{roleId}' 不存在");
        }

        var validationErrors = policy.ValidateDelete(role);
        if (validationErrors.Length > 0)
        {
            return RoleManagementCommandResult<RoleResponse>.Failure(RoleManagementCommandStatus.Protected, validationErrors);
        }

        var response = await ToResponseAsync(role, includePermissions: true);
        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return FromIdentityFailure<RoleResponse>(RoleManagementCommandStatus.ValidationFailed, result);
        }

        return RoleManagementCommandResult<RoleResponse>.Success(response);
    }

    public async Task<RoleManagementCommandResult<IReadOnlyList<string>>> SyncPermissionsAsync(
        Guid roleId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return RoleManagementCommandResult<IReadOnlyList<string>>.Failure(
                RoleManagementCommandStatus.NotFound,
                $"角色 '{roleId}' 不存在");
        }

        var targetSet = permissions
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(static permission => permission.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidPermissions = await permissionCatalogService.ValidateAssignablePermissionsAsync(targetSet, cancellationToken);
        if (invalidPermissions.Length > 0)
        {
            return RoleManagementCommandResult<IReadOnlyList<string>>.Failure(
                RoleManagementCommandStatus.ValidationFailed,
                $"以下权限不存在或未启用：{string.Join(", ", invalidPermissions)}");
        }

        var existingClaims = await roleManager.GetClaimsAsync(role);
        var existingSet = existingClaims
            .Where(static claim => string.Equals(claim.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(static claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in targetSet.Except(existingSet, StringComparer.OrdinalIgnoreCase))
        {
            var addResult = await roleManager.AddClaimAsync(role, new Claim(PermissionConstants.ClaimType, permission));
            if (!addResult.Succeeded)
            {
                return FromIdentityFailure<IReadOnlyList<string>>(RoleManagementCommandStatus.ValidationFailed, addResult);
            }
        }

        foreach (var permission in existingSet.Except(targetSet, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var claim in existingClaims.Where(claim =>
                         string.Equals(claim.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase)))
            {
                var removeResult = await roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    return FromIdentityFailure<IReadOnlyList<string>>(RoleManagementCommandStatus.ValidationFailed, removeResult);
                }
            }
        }

        return RoleManagementCommandResult<IReadOnlyList<string>>.Success(await GetPermissionClaimsAsync(role));
    }

    private async Task<RoleResponse> ToResponseAsync(BzsRole role, bool includePermissions)
    {
        var permissions = await GetPermissionClaimsAsync(role);
        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name ?? role.Id.ToString(),
            NormalizedName = role.NormalizedName ?? string.Empty,
            IsProtected = policy.IsProtectedRole(role),
            PermissionCount = permissions.Count,
            Permissions = includePermissions ? permissions.ToArray() : [],
        };
    }

    private async Task<IReadOnlyList<string>> GetPermissionClaimsAsync(BzsRole role)
    {
        var claims = await roleManager.GetClaimsAsync(role);
        return claims
            .Where(static claim => string.Equals(claim.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(static claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static RoleManagementCommandResult<T> FromIdentityFailure<T>(
        RoleManagementCommandStatus status,
        IdentityResult result)
    {
        return RoleManagementCommandResult<T>.Failure(
            status,
            result.Errors.Select(static error => error.Description).ToArray());
    }
}
