using System.Security.Claims;
using BzsCenter.Idp.Models;
using BzsCenter.Idp.Services.Oidc;
using BzsCenter.Idp.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using SharedPermissionConstants = BzsCenter.Shared.Infrastructure.Authorization.PermissionConstants;

namespace BzsCenter.Idp.Controllers;

[ApiController]
public sealed class ConnectController(
    SignInManager<BzsUser> signInManager,
    UserManager<BzsUser> userManager,
    IOpenIddictApplicationManager applicationManager,
    IPermissionScopeService permissionScopeService,
    IOidcPrincipalFactory oidcPrincipalFactory) : ControllerBase
{
    /// <summary>
    /// 处理授权流程。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = GetRequiredOpenIddictRequest();

        var cookieAuthResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!cookieAuthResult.Succeeded || cookieAuthResult.Principal is null)
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + Request.QueryString,
            }, IdentityConstants.ApplicationScheme);
        }

        var user = await userManager.GetUserAsync(cookieAuthResult.Principal);
        if (user is null)
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + Request.QueryString,
            }, IdentityConstants.ApplicationScheme);
        }

        if (!await signInManager.CanSignInAsync(user))
        {
            return Forbid(IdentityConstants.ApplicationScheme);
        }

        var principal = await oidcPrincipalFactory.CreateUserPrincipalAsync(user);
        principal.SetScopes(oidcPrincipalFactory.FilterRequestedScopes(request.GetScopes()));
        await PermissionClaimDestinationsHandler.ApplyDestinationsAsync(principal, permissionScopeService, cancellationToken);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// 处理令牌交换流程。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        var request = GetRequiredOpenIddictRequest();

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = authResult.Principal;
            if (principal is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var subject = principal.GetClaim(OpenIddictConstants.Claims.Subject);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var user = await userManager.FindByIdAsync(subject);
            if (user is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            if (!await signInManager.CanSignInAsync(user))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var refreshedPrincipal = await oidcPrincipalFactory.CreateUserPrincipalAsync(user);
            refreshedPrincipal.SetScopes(principal.GetScopes());
            await PermissionClaimDestinationsHandler.ApplyDestinationsAsync(refreshedPrincipal, permissionScopeService,
                cancellationToken);

            return SignIn(refreshedPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            if (string.IsNullOrWhiteSpace(request.ClientId))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var application = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
            if (application is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var displayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken);
            var principal = oidcPrincipalFactory.CreateClientPrincipal(request.ClientId, displayName);
            principal.SetScopes(oidcPrincipalFactory.FilterRequestedScopes(request.GetScopes()));
            await PermissionClaimDestinationsHandler.ApplyDestinationsAsync(principal, permissionScopeService,
                cancellationToken);

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new
        {
            error = OpenIddictConstants.Errors.UnsupportedGrantType,
            error_description = "The specified grant type is not supported.",
        });
    }

    /// <summary>
    /// 返回用户信息。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<IActionResult> UserInfo(CancellationToken cancellationToken)
    {
        var subject = User.GetClaim(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var roles = User.Claims
            .Where(static claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, OpenIddictConstants.Claims.Role, StringComparison.OrdinalIgnoreCase))
            .Select(static c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissions = User.FindAll(SharedPermissionConstants.ClaimType)
            .Select(static c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var response = new Dictionary<string, object?>
        {
            [OpenIddictConstants.Claims.Subject] = subject,
            [OpenIddictConstants.Claims.Name] = User.GetClaim(OpenIddictConstants.Claims.Name)
                                                 ?? User.Identity?.Name
                                                 ?? (string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName),
            [OpenIddictConstants.Claims.Email] = User.FindFirstValue(ClaimTypes.Email) ?? user.Email,
            [OpenIddictConstants.Claims.Role] = roles,
            [SharedPermissionConstants.ClaimType] = permissions,
        };

        return Ok(response
            .Where(static pair => pair.Value is not null)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value));
    }

    /// <summary>
    /// 处理注销流程。
    /// </summary>
    /// <returns>执行结果。</returns>
    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public IActionResult Logout()
    {
        var request = HttpContext.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction?.Request;
        var redirectUri = string.IsNullOrWhiteSpace(request?.PostLogoutRedirectUri)
            ? "/"
            : request.PostLogoutRedirectUri;

        return SignOut(
            new AuthenticationProperties { RedirectUri = redirectUri },
            IdentityConstants.ApplicationScheme,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <returns>执行结果。</returns>
    private OpenIddictRequest GetRequiredOpenIddictRequest()
    {
        var request = HttpContext.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction?.Request;
        if (request is null)
        {
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
        }

        return request;
    }
}
