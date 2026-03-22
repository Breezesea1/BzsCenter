using System.Security.Claims;
using BzsCenter.Idp.Models;
using Microsoft.AspNetCore.Identity;

namespace BzsCenter.Idp.Services.Identity;

public readonly record struct ExternalLoginResult(bool Succeeded, string? ErrorCode)
{
    internal static ExternalLoginResult Failed(string errorCode = "external_login_failed") => new(false, errorCode);
    internal static ExternalLoginResult Success() => new(true, null);
}

public interface IExternalLoginService
{
    Task<ExternalLoginResult> SignInAsync(CancellationToken cancellationToken = default);
}

internal sealed class ExternalLoginService(
    UserManager<BzsUser> userManager,
    SignInManager<BzsUser> signInManager) : IExternalLoginService
{
    public async Task<ExternalLoginResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        var loginInfo = await signInManager.GetExternalLoginInfoAsync();
        if (loginInfo is null)
        {
            return ExternalLoginResult.Failed();
        }

        var signInResult = await signInManager.ExternalLoginSignInAsync(loginInfo.LoginProvider, loginInfo.ProviderKey, false, true);
        if (signInResult.Succeeded)
        {
            return ExternalLoginResult.Success();
        }

        var user = await ResolveUserAsync(loginInfo, cancellationToken);
        if (user is null)
        {
            return ExternalLoginResult.Failed();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return ExternalLoginResult.Failed();
        }

        var addLoginResult = await userManager.AddLoginAsync(user, loginInfo);
        if (!addLoginResult.Succeeded)
        {
            return ExternalLoginResult.Failed();
        }

        await signInManager.SignInAsync(user, false, loginInfo.LoginProvider);
        return ExternalLoginResult.Success();
    }

    private async Task<BzsUser?> ResolveUserAsync(ExternalLoginInfo loginInfo, CancellationToken cancellationToken)
    {
        var email = loginInfo.Principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                return existingUser;
            }
        }

        var userName = await GenerateUserNameAsync(loginInfo, cancellationToken);
        var user = new BzsUser
        {
            UserName = userName,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            EmailConfirmed = !string.IsNullOrWhiteSpace(email),
        };

        var createResult = await userManager.CreateAsync(user);
        return createResult.Succeeded ? user : null;
    }

    private async Task<string> GenerateUserNameAsync(ExternalLoginInfo loginInfo, CancellationToken cancellationToken)
    {
        var baseUserName = loginInfo.Principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(baseUserName))
        {
            var email = loginInfo.Principal.FindFirstValue(ClaimTypes.Email);
            baseUserName = !string.IsNullOrWhiteSpace(email)
                ? email.Split('@', 2)[0]
                : $"{loginInfo.LoginProvider.ToLowerInvariant()}-{loginInfo.ProviderKey}";
        }

        var normalizedBaseUserName = new string(baseUserName
            .Trim()
            .Where(static character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            .ToArray());

        if (string.IsNullOrWhiteSpace(normalizedBaseUserName))
        {
            normalizedBaseUserName = $"{loginInfo.LoginProvider.ToLowerInvariant()}-{loginInfo.ProviderKey}";
        }

        for (var suffix = 0; suffix < 100; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = suffix == 0 ? normalizedBaseUserName : $"{normalizedBaseUserName}-{suffix}";
            var existingUser = await userManager.FindByNameAsync(candidate);
            if (existingUser is null)
            {
                return candidate;
            }
        }

        return $"{loginInfo.LoginProvider.ToLowerInvariant()}-{Guid.NewGuid():N}";
    }
}
