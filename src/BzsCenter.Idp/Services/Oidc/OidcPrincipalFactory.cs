using System.Security.Claims;
using BzsCenter.Idp.Models;
using BzsCenter.Idp.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace BzsCenter.Idp.Services.Oidc;

public interface IOidcPrincipalFactory
{
    IReadOnlyList<string> FilterRequestedScopes(IEnumerable<string> requestedScopes);
    Task<ClaimsPrincipal> CreateUserPrincipalAsync(BzsUser user);
    ClaimsPrincipal CreateClientPrincipal(string clientId, string? displayName);
}

internal sealed class OidcPrincipalFactory(
    SignInManager<BzsUser> signInManager,
    UserManager<BzsUser> userManager,
    IOptions<IdentitySeedOptions> identityOptions) : IOidcPrincipalFactory
{
    public IReadOnlyList<string> FilterRequestedScopes(IEnumerable<string> requestedScopes)
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

    public async Task<ClaimsPrincipal> CreateUserPrincipalAsync(BzsUser user)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        principal.SetClaim(OpenIddictConstants.Claims.Subject, await userManager.GetUserIdAsync(user));

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.UserName
            : user.DisplayName;

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            principal.SetClaim(OpenIddictConstants.Claims.Name, displayName);
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

    public ClaimsPrincipal CreateClientPrincipal(string clientId, string? displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.SetClaim(OpenIddictConstants.Claims.Subject, clientId);

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            identity.SetClaim(OpenIddictConstants.Claims.Name, displayName);
        }

        return new ClaimsPrincipal(identity);
    }

    private static void RemoveClaims(ClaimsIdentity identity, params string[] claimTypes)
    {
        foreach (var claim in identity.Claims
                     .Where(claim => claimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
                     .ToArray())
        {
            identity.RemoveClaim(claim);
        }
    }
}
