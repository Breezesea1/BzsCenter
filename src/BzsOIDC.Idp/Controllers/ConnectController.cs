using System.Collections.Immutable;
using System.Security.Claims;
using BzsOIDC.Idp.Models;
using BzsOIDC.Idp.Services.Oidc;
using BzsOIDC.Idp.Services.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using SharedPermissionConstants = BzsOIDC.Shared.Infrastructure.Authorization.PermissionConstants;

namespace BzsOIDC.Idp.Controllers;

[ApiController]
public sealed class ConnectController(
    SignInManager<BzsUser> signInManager,
    UserManager<BzsUser> userManager,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IAntiforgery antiforgery,
    IOidcConsentPageRenderer consentPageRenderer,
    IPermissionCatalogService permissionCatalogService,
    IOidcPrincipalFactory oidcPrincipalFactory) : ControllerBase
{
    /// <summary>
    /// 处理授权流程。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
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

        if (HttpMethods.IsPost(Request.Method) && !await ValidateAntiforgeryRequestAsync())
        {
            return BadRequest(new
            {
                error = OpenIddictConstants.Errors.InvalidRequest,
                error_description = "The authorization request could not be validated.",
            });
        }

        var principal = await oidcPrincipalFactory.CreateUserPrincipalAsync(user);
        var scopes = oidcPrincipalFactory.FilterRequestedScopes(request.GetScopes()).ToArray();
        principal.SetScopes(scopes);
        await PermissionClaimDestinationsHandler.ApplyDestinationsAsync(principal, permissionCatalogService, cancellationToken);

        var consentOutcome = await TryApplyConsentAsync(request, user, principal, scopes, cancellationToken);
        if (consentOutcome == OidcConsentOutcome.Authorized)
        {
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (consentOutcome == OidcConsentOutcome.ConsentRequired)
        {
            return Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [".error"] = OpenIddictConstants.Errors.ConsentRequired,
                    [".error_description"] = "The authorization request requires the user to consent.",
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (HttpMethods.IsPost(Request.Method))
        {
            if (string.Equals(Request.Form["consent"], "deny", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [".error"] = OpenIddictConstants.Errors.AccessDenied,
                        [".error_description"] = "The resource owner denied the authorization request.",
                    }),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            if (string.Equals(Request.Form["consent"], "accept", StringComparison.OrdinalIgnoreCase))
            {
                await CreateAndAttachAuthorizationAsync(request, user, principal, scopes, cancellationToken);
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
        }

        var clientDisplayName = await ResolveClientDisplayNameAsync(request, cancellationToken);
        return consentPageRenderer.Render(HttpContext, Request.Query, clientDisplayName, scopes);
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

            var authorizationId = principal.GetAuthorizationId();
            if (!string.IsNullOrWhiteSpace(authorizationId))
            {
                refreshedPrincipal.SetAuthorizationId(authorizationId);
            }

            await PermissionClaimDestinationsHandler.ApplyDestinationsAsync(refreshedPrincipal, permissionCatalogService,
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
            await PermissionClaimDestinationsHandler.ApplyDestinationsAsync(principal, permissionCatalogService,
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

    private async Task<bool> ValidateAntiforgeryRequestAsync()
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private async Task<OidcConsentOutcome> TryApplyConsentAsync(
        OpenIddictRequest request,
        BzsUser user,
        ClaimsPrincipal principal,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return OidcConsentOutcome.Authorized;
        }

        var application = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
        if (application is null)
        {
            return OidcConsentOutcome.Authorized;
        }

        if (request.HasPromptValue("consent"))
        {
            return OidcConsentOutcome.ConsentPageRequired;
        }

        var consentType = await applicationManager.GetConsentTypeAsync(application, cancellationToken);
        if (!string.Equals(consentType, OpenIddictConstants.ConsentTypes.Explicit, StringComparison.OrdinalIgnoreCase))
        {
            return OidcConsentOutcome.Authorized;
        }

        var applicationId = await applicationManager.GetIdAsync(application, cancellationToken);
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return OidcConsentOutcome.Authorized;
        }

        var subject = await userManager.GetUserIdAsync(user);
        var authorizations = authorizationManager.FindAsync(
            subject,
            applicationId,
            OpenIddictConstants.Statuses.Valid,
            OpenIddictConstants.AuthorizationTypes.Permanent,
            // Consent is intentionally scope-specific: expanding requested scopes must prompt again.
            scopes.ToImmutableArray(),
            cancellationToken);

        await foreach (var authorization in authorizations)
        {
            var authorizationId = await authorizationManager.GetIdAsync(authorization, cancellationToken);
            if (!string.IsNullOrWhiteSpace(authorizationId))
            {
                principal.SetAuthorizationId(authorizationId);
                return OidcConsentOutcome.Authorized;
            }
        }

        return request.HasPromptValue("none")
            ? OidcConsentOutcome.ConsentRequired
            : OidcConsentOutcome.ConsentPageRequired;
    }

    private async Task CreateAndAttachAuthorizationAsync(
        OpenIddictRequest request,
        BzsUser user,
        ClaimsPrincipal principal,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        var application = string.IsNullOrWhiteSpace(request.ClientId)
            ? null
            : await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);

        var applicationId = application is null
            ? null
            : await applicationManager.GetIdAsync(application, cancellationToken);

        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new InvalidOperationException("An explicit consent authorization requires a valid OIDC application.");
        }

        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = applicationId,
            Principal = principal,
            Status = OpenIddictConstants.Statuses.Valid,
            Subject = await userManager.GetUserIdAsync(user),
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
        };

        descriptor.Scopes.UnionWith(scopes);

        var authorization = await authorizationManager.CreateAsync(descriptor, cancellationToken);

        var authorizationId = await authorizationManager.GetIdAsync(authorization, cancellationToken);
        if (!string.IsNullOrWhiteSpace(authorizationId))
        {
            principal.SetAuthorizationId(authorizationId);
        }
    }

    private async Task<string> ResolveClientDisplayNameAsync(OpenIddictRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            var application = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
            if (application is not null)
            {
                var displayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }

        return request.ClientId ?? "the client";
    }

    private enum OidcConsentOutcome
    {
        Authorized,
        ConsentPageRequired,
        ConsentRequired,
    }
}
