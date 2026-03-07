using BzsCenter.Idp.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BzsCenter.Idp.Services.Identity;

public interface IUserService
{
    Task<IReadOnlyList<BzsUser>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BzsUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BzsUser?> GetByNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<IdentityResult> CreateAsync(string userName, string password, string? email = null,
        CancellationToken cancellationToken = default);

    Task<IdentityResult> UpdateAsync(Guid userId, string userName, string? email = null,
        CancellationToken cancellationToken = default);

    Task<IdentityResult> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityResult> AddToRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<bool> IsInRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
}

internal sealed class UserService(UserManager<BzsUser> userManager) : IUserService
{
    public async Task<IReadOnlyList<BzsUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);
    }

    public Task<BzsUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public Task<BzsUser?> GetByNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        return userManager.FindByNameAsync(userName);
    }

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

    public async Task<IdentityResult> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityResult.Failed(CreateError("UserNotFound", $"用户 '{userId}' 不存在"));
        }

        return await userManager.DeleteAsync(user);
    }

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

    private static IdentityError CreateError(string code, string description)
    {
        return new IdentityError
        {
            Code = code,
            Description = description,
        };
    }
}
