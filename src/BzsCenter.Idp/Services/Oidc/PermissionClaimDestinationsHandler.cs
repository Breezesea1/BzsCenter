using System.Security.Claims;
using BzsCenter.Idp.Services.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using SharedPermissionConstants = BzsCenter.Shared.Infrastructure.Authorization.PermissionConstants;

namespace BzsCenter.Idp.Services.Oidc;

internal sealed class PermissionClaimDestinationsHandler(IPermissionScopeService permissionScopeService)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>
    /// 执行HandleAsync。
    /// </summary>
    /// <param name="context">参数context。</param>
    /// <returns>执行结果。</returns>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.Principal is null)
        {
            return;
        }

        var permissionValues = context.Principal
            .FindAll(SharedPermissionConstants.ClaimType)
            .Select(static claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissionScopes = await permissionScopeService.ResolveScopesAsync(permissionValues, context.CancellationToken);
        var grantedScopes = context.Principal.GetScopes().ToHashSet(StringComparer.OrdinalIgnoreCase);

        context.Principal.SetDestinations(claim =>
        {
            if (string.Equals(claim.Type, SharedPermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase))
            {
                if (ShouldEmitPermissionClaim(claim.Value, grantedScopes, permissionScopes))
                {
                    return [OpenIddictConstants.Destinations.AccessToken];
                }

                return [];
            }

            if (string.Equals(claim.Type, OpenIddictConstants.Claims.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, ClaimTypes.Name, StringComparison.OrdinalIgnoreCase))
            {
                return grantedScopes.Contains(OpenIddictConstants.Scopes.Profile)
                    ? [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]
                    : [OpenIddictConstants.Destinations.AccessToken];
            }

            if (string.Equals(claim.Type, OpenIddictConstants.Claims.Email, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, ClaimTypes.Email, StringComparison.OrdinalIgnoreCase))
            {
                return grantedScopes.Contains(OpenIddictConstants.Scopes.Email)
                    ? [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]
                    : [OpenIddictConstants.Destinations.AccessToken];
            }

            if (string.Equals(claim.Type, OpenIddictConstants.Claims.Role, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
            {
                return grantedScopes.Contains(OpenIddictConstants.Scopes.Roles)
                    ? [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]
                    : [];
            }

            if (string.Equals(claim.Type, OpenIddictConstants.Claims.Subject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken];
            }

            return [];
        });
    }

    /// <summary>
    /// 执行ShouldEmitPermissionClaim。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <param name="grantedScopes">参数grantedScopes。</param>
    /// <param name="permissionScopes">参数permissionScopes。</param>
    /// <returns>执行结果。</returns>
    private static bool ShouldEmitPermissionClaim(
        string permission,
        ISet<string> grantedScopes,
        IReadOnlyDictionary<string, string[]> permissionScopes)
    {
        if (!permissionScopes.TryGetValue(permission, out var requiredScopes) || requiredScopes.Length == 0)
        {
            return false;
        }

        return requiredScopes.Any(grantedScopes.Contains);
    }
}
