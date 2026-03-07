using System.Security.Claims;
using BzsCenter.Idp.Domain;
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
    IOptions<IdentitySeedOptions> identityOptions) : ControllerBase
{
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

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        principal.SetScopes(FilterRequestedScopes(request.GetScopes()));

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

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

            var refreshedPrincipal = await signInManager.CreateUserPrincipalAsync(user);
            refreshedPrincipal.SetScopes(principal.GetScopes());

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

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new
        {
            error = OpenIddictConstants.Errors.UnsupportedGrantType,
            error_description = "The specified grant type is not supported.",
        });
    }

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

        var roles = User.FindAll(ClaimTypes.Role)
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

        return Ok(response.Where(static pair => pair.Value is not null));
    }

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
