using System.Security.Claims;
using BzsCenter.Idp.Domain;
using BzsCenter.Idp.Services.Oidc;
using BzsCenter.Idp.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
    IOptions<IdentitySeedOptions> identityOptions) : ControllerBase
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

        var principal = await CreateOidcPrincipalAsync(user);
        principal.SetScopes(FilterRequestedScopes(request.GetScopes()));
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

            var refreshedPrincipal = await CreateOidcPrincipalAsync(user);
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

            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.SetClaim(OpenIddictConstants.Claims.Subject, request.ClientId);

            var displayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                identity.SetClaim(OpenIddictConstants.Claims.Name, displayName);
            }

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(FilterRequestedScopes(request.GetScopes()));
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
            [OpenIddictConstants.Claims.Name] = User.Identity?.Name ?? user.UserName,
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
    /// 过滤并返回结果。
    /// </summary>
    /// <param name="requestedScopes">参数requestedScopes。</param>
    /// <returns>执行结果。</returns>
    private IReadOnlyList<string> FilterRequestedScopes(IEnumerable<string> requestedScopes)
    {
        var allowedScopes = identityOptions.Value.AdditionalScopes
            .Append(OpenIddictConstants.Scopes.OpenId)
            .Append(OpenIddictConstants.Scopes.Profile)
            .Append(OpenIddictConstants.Scopes.Email)
            .Append(OpenIddictConstants.Scopes.Roles)
            .Append(OpenIddictConstants.Scopes.OfflineAccess)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requestedScopes
            .Where(scope => allowedScopes.Contains(scope))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 创建适用于 OIDC 签发的 principal。
    /// </summary>
    /// <param name="user">参数user。</param>
    /// <returns>执行结果。</returns>
    private async Task<ClaimsPrincipal> CreateOidcPrincipalAsync(BzsUser user)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        principal.SetClaim(OpenIddictConstants.Claims.Subject, await userManager.GetUserIdAsync(user));

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            principal.SetClaim(OpenIddictConstants.Claims.Name, user.UserName);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            principal.SetClaim(OpenIddictConstants.Claims.Email, user.Email);
        }

        var identity = principal.Identities.FirstOrDefault();
        if (identity is null)
        {
            return principal;
        }

        RemoveClaims(identity, ClaimTypes.Name, ClaimTypes.Email, ClaimTypes.Role);

        var existingRoles = identity.FindAll(OpenIddictConstants.Claims.Role)
            .Select(static claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in (await userManager.GetRolesAsync(user)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (existingRoles.Add(roleName))
            {
                identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, roleName));
            }
        }

        return principal;
    }

    /// <summary>
    /// 删除同类型旧 claims，避免在 token 中重复发放。
    /// </summary>
    /// <param name="identity">参数identity。</param>
    /// <param name="claimTypes">参数claimTypes。</param>
    private static void RemoveClaims(ClaimsIdentity identity, params string[] claimTypes)
    {
        foreach (var claim in identity.Claims
                     .Where(claim => claimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
                     .ToArray())
        {
            identity.RemoveClaim(claim);
        }
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
