using System.Security.Claims;
using BzsCenter.Idp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

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
    SignInManager<BzsUser> signInManager,
    ILogger<ExternalLoginService> logger) : IExternalLoginService
{
    public async Task<ExternalLoginResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        var loginInfo = await signInManager.GetExternalLoginInfoAsync();
        if (loginInfo is null)
        {
            logger.LogWarning("External login callback did not contain external login info.");
            return ExternalLoginResult.Failed();
        }

        var signInResult = await signInManager.ExternalLoginSignInAsync(loginInfo.LoginProvider, loginInfo.ProviderKey, false, true);
        if (signInResult.Succeeded)
        {
            return ExternalLoginResult.Success();
        }

        logger.LogInformation("External login sign-in did not match an existing linked login for provider '{Provider}'. Falling back to user resolution.", loginInfo.LoginProvider);

        var user = await ResolveUserAsync(loginInfo, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("External login user resolution failed for provider '{Provider}' and key '{ProviderKey}'.", loginInfo.LoginProvider, loginInfo.ProviderKey);
            return ExternalLoginResult.Failed();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("External login matched locked out user '{UserId}'.", user.Id);
            return ExternalLoginResult.Failed();
        }

        var addLoginResult = await userManager.AddLoginAsync(user, loginInfo);
        if (!addLoginResult.Succeeded)
        {
            logger.LogWarning("External login could not be linked to user '{UserId}'. Errors: {Errors}", user.Id, string.Join(", ", addLoginResult.Errors.Select(static error => error.Code)));
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
        var user = BzsUser.CreateExternal(userName, email, ResolveDisplayName(loginInfo.Principal));

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            logger.LogWarning("External login could not create a local user for provider '{Provider}'. Errors: {Errors}", loginInfo.LoginProvider, string.Join(", ", createResult.Errors.Select(static error => error.Code)));
        }

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

    private static string? ResolveDisplayName(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("urn:github:name")
               ?? principal.FindFirstValue(ClaimTypes.GivenName)
               ?? principal.FindFirstValue(ClaimTypes.Name);
    }
}
