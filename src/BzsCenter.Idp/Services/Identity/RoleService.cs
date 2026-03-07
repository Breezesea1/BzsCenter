using BzsCenter.Idp.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BzsCenter.Idp.Services.Identity;

public interface IRoleService
{
    Task<IReadOnlyList<BzsRole>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BzsRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<BzsRole?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<IdentityResult> CreateAsync(string roleName, CancellationToken cancellationToken = default);
    Task<IdentityResult> UpdateAsync(Guid roleId, string roleName, CancellationToken cancellationToken = default);
    Task<IdentityResult> DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);
}

internal sealed class RoleService(RoleManager<BzsRole> roleManager) : IRoleService
{
    public async Task<IReadOnlyList<BzsRole>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<BzsRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return roleManager.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
    }

    public Task<BzsRole?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        return roleManager.FindByNameAsync(roleName);
    }

    public async Task<IdentityResult> CreateAsync(string roleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var role = new BzsRole
        {
            Name = roleName.Trim(),
            NormalizedName = roleName.Trim().ToUpperInvariant(),
        };

        return await roleManager.CreateAsync(role);
    }

    public async Task<IdentityResult> UpdateAsync(Guid roleId, string roleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        var normalizedTargetName = roleName.Trim();

        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return IdentityResult.Failed(CreateError("RoleNotFound", $"角色 '{roleId}' 不存在"));
        }

        if (string.Equals(role.Name, IdentitySeedConstants.AdminRoleName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedTargetName, IdentitySeedConstants.AdminRoleName, StringComparison.OrdinalIgnoreCase))
        {
            return IdentityResult.Failed(CreateError("ProtectedRole", "admin 角色不允许重命名"));
        }

        if (!string.Equals(role.Name, IdentitySeedConstants.AdminRoleName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedTargetName, IdentitySeedConstants.AdminRoleName, StringComparison.OrdinalIgnoreCase))
        {
            return IdentityResult.Failed(CreateError("ReservedRole", "admin 为保留角色名，不能通过重命名创建"));
        }

        role.Name = normalizedTargetName;
        role.NormalizedName = normalizedTargetName.ToUpperInvariant();

        return await roleManager.UpdateAsync(role);
    }

    public async Task<IdentityResult> DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return IdentityResult.Failed(CreateError("RoleNotFound", $"角色 '{roleId}' 不存在"));
        }

        if (string.Equals(role.Name, IdentitySeedConstants.AdminRoleName, StringComparison.OrdinalIgnoreCase))
        {
            return IdentityResult.Failed(CreateError("ProtectedRole", "admin 角色不允许删除"));
        }

        return await roleManager.DeleteAsync(role);
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
