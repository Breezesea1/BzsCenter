using BzsCenter.Shared.Infrastructure.Authorization;

namespace BzsCenter.Idp.Services.Identity;

public static class SmokeIdentitySeedProfile
{
    public static IdentitySeedOptions Resolve(bool smokeEnabled, IdentitySeedOptions baseOptions)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);

        if (!smokeEnabled)
        {
            return baseOptions;
        }

        return new IdentitySeedOptions
        {
            Admin = baseOptions.Admin,
            InitialRoles = [],
            RolePermissions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            PermissionScopes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [PermissionConstants.UsersReadAll] = [PermissionConstants.ScopeApi],
                [PermissionConstants.UsersWrite] = [PermissionConstants.ScopeApi],
                [PermissionConstants.ClientsRead] = [PermissionConstants.ScopeApi],
                [PermissionConstants.ClientsWrite] = [PermissionConstants.ScopeApi],
            },
            AdditionalScopes = [PermissionConstants.ScopeApi],
        };
    }
}
