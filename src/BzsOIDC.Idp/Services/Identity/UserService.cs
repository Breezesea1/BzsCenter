using BzsOIDC.Idp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BzsOIDC.Idp.Services.Identity;

public interface IUserService
{
    Task<IReadOnlyList<BzsUser>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BzsUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BzsUser?> GetByNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<IdentityResult> CreateAsync(string userName, string password, string? email = null,
        CancellationToken cancellationToken = default);
    Task<IdentityResult> EnsurePasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);

    Task<IdentityResult> UpdateAsync(Guid userId, string userName, string? email = null,
        CancellationToken cancellationToken = default);

    Task<IdentityResult> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityResult> AddToRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<bool> IsInRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
}

internal sealed class UserService(UserManager<BzsUser> userManager) : IUserService
{
    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IReadOnlyList<BzsUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="userId">参数userId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public Task<BzsUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="userName">参数userName。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public Task<BzsUser?> GetByNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        return userManager.FindByNameAsync(userName);
    }

    /// <summary>
    /// 创建数据。
    /// </summary>
    /// <param name="userName">参数userName。</param>
    /// <param name="password">参数password。</param>
    /// <param name="email">参数email。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IdentityResult> CreateAsync(string userName, string password, string? email = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var user = new BzsUser
        {
            UserName = userName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
        };

        return await userManager.CreateAsync(user, password);
    }

    /// <summary>
    /// 确保用户密码与给定密码一致。
    /// </summary>
    /// <param name="userId">参数userId。</param>
    /// <param name="password">参数password。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IdentityResult> EnsurePasswordAsync(Guid userId, string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityResult.Failed(CreateError("UserNotFound", $"用户 '{userId}' 不存在"));
        }

        if (await userManager.CheckPasswordAsync(user, password))
        {
            return IdentityResult.Success;
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        return await userManager.ResetPasswordAsync(user, resetToken, password);
    }

    /// <summary>
    /// 更新数据。
    /// </summary>
    /// <param name="userId">参数userId。</param>
    /// <param name="userName">参数userName。</param>
    /// <param name="email">参数email。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IdentityResult> UpdateAsync(Guid userId, string userName, string? email = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityResult.Failed(CreateError("UserNotFound", $"用户 '{userId}' 不存在"));
        }

        user.UserName = userName.Trim();
        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        return await userManager.UpdateAsync(user);
    }

    /// <summary>
    /// 删除数据。
    /// </summary>
    /// <param name="userId">参数userId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IdentityResult> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityResult.Failed(CreateError("UserNotFound", $"用户 '{userId}' 不存在"));
        }

        return await userManager.DeleteAsync(user);
    }

    /// <summary>
    /// 添加数据。
    /// </summary>
    /// <param name="userId">参数userId。</param>
    /// <param name="roleName">参数roleName。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IdentityResult> AddToRoleAsync(Guid userId, string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityResult.Failed(CreateError("UserNotFound", $"用户 '{userId}' 不存在"));
        }

        return await userManager.AddToRoleAsync(user, roleName.Trim());
    }

    /// <summary>
    /// 判断条件是否成立。
    /// </summary>
    /// <param name="userId">参数userId。</param>
    /// <param name="roleName">参数roleName。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<bool> IsInRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        return await userManager.IsInRoleAsync(user, roleName.Trim());
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
