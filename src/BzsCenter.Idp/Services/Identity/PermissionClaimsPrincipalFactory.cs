using System.Security.Claims;
using BzsCenter.Idp.Domain;
using BzsCenter.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BzsCenter.Idp.Services.Identity;

internal sealed class PermissionClaimsPrincipalFactory(
    UserManager<BzsUser> userManager,
    RoleManager<BzsRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<BzsUser, BzsRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(BzsUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var roleNames = await UserManager.GetRolesAsync(user);
        if (roleNames.Count == 0)
        {
            return identity;
        }

        var existingPermissions = identity.Claims
            .Where(static c => string.Equals(c.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(static c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roleNames)
        {
            var role = await RoleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var roleClaims = await RoleManager.GetClaimsAsync(role);
            foreach (var permissionClaim in roleClaims.Where(static c =>
                         string.Equals(c.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase)))
            {
                if (existingPermissions.Add(permissionClaim.Value))
                {
                    identity.AddClaim(permissionClaim);
                }
            }
        }

        return identity;
    }
}
