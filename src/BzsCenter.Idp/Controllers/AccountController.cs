using BzsCenter.Idp.Models;
using BzsCenter.Idp.Services.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace BzsCenter.Idp.Controllers;

[Route("account")]
public sealed class AccountController(
    SignInManager<BzsUser> signInManager,
    IExternalLoginService externalLoginService,
    IExternalLoginProviderStore externalLoginProviderStore) : Controller
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginForm form, [FromQuery] string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(form.UserName) || string.IsNullOrWhiteSpace(form.Password))
        {
            return RedirectToLogin(returnUrl, "validation");
        }

        var userName = form.UserName.Trim();
        var result = await signInManager.PasswordSignInAsync(userName, form.Password, form.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return RedirectToSafeLocal(returnUrl);
        }

        return RedirectToLogin(returnUrl, "invalid_credentials");
    }

    [HttpPost("logout")]
    [IgnoreAntiforgeryToken]
    public IActionResult Logout([FromQuery] string? returnUrl)
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = IsSafeLocalUrl(returnUrl) ? returnUrl : "/" },
            IdentityConstants.ApplicationScheme);
    }

    [HttpPost("external-login/{provider}")]
    public IActionResult ExternalLogin([FromRoute] string provider, [FromQuery] string? returnUrl)
    {
        if (!externalLoginProviderStore.TryGetProvider(provider, out var externalLoginProvider))
        {
            return RedirectToLogin(returnUrl, "external_login_failed");
        }

        var callbackUrl = BuildExternalLoginCallbackUrl(returnUrl);
        var properties = signInManager.ConfigureExternalAuthenticationProperties(externalLoginProvider.Scheme, callbackUrl);
        return Challenge(properties, externalLoginProvider.Scheme);
    }

    [HttpGet("external-login/callback")]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await externalLoginService.SignInAsync(cancellationToken);
        if (result.Succeeded)
        {
            return RedirectToSafeLocal(returnUrl);
        }

        return RedirectToLogin(returnUrl, result.ErrorCode ?? "external_login_failed");
    }

    private IActionResult RedirectToLogin(string? returnUrl, string error)
    {
        var queryValues = new Dictionary<string, string?>
        {
            ["error"] = error,
        };

        if (IsSafeLocalUrl(returnUrl))
        {
            queryValues["returnUrl"] = returnUrl;
        }

        IDictionary<string, string?> safeQueryValues = queryValues;
        var path = QueryHelpers.AddQueryString(
            "/login",
            safeQueryValues
                .Where(static item => !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(static item => item.Key, static item => item.Value));

        return Redirect(path);
    }
    private IActionResult RedirectToSafeLocal(string? returnUrl)
    {
        if (IsSafeLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl!);
        }

        return Redirect("/");
    }

    private bool IsSafeLocalUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl);
    }

    private string BuildExternalLoginCallbackUrl(string? returnUrl)
    {
        if (!IsSafeLocalUrl(returnUrl))
        {
            return "/account/external-login/callback";
        }

        return QueryHelpers.AddQueryString("/account/external-login/callback", new Dictionary<string, string?>
        {
            ["returnUrl"] = returnUrl,
        });
    }

    public sealed class LoginForm
    {
        public string UserName { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public bool RememberMe { get; init; }
    }
}
