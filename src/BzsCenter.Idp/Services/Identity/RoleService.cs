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
    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IReadOnlyList<BzsRole>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="roleId">参数roleId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public Task<BzsRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return roleManager.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="roleName">参数roleName。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public Task<BzsRole?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        return roleManager.FindByNameAsync(roleName);
    }

    /// <summary>
    /// 创建数据。
    /// </summary>
    /// <param name="roleName">参数roleName。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
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

    /// <summary>
    /// 更新数据。
    /// </summary>
    /// <param name="roleId">参数roleId。</param>
    /// <param name="roleName">参数roleName。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
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

    /// <summary>
    /// 删除数据。
    /// </summary>
    /// <param name="roleId">参数roleId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
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

    /// <summary>
    /// 创建数据。
    /// </summary>
    /// <param name="code">参数code。</param>
    /// <param name="description">参数description。</param>
    /// <returns>执行结果。</returns>
    private static IdentityError CreateError(string code, string description)
    {
        return new IdentityError
        {
            Code = code,
            Description = description,
        };
    }
}
