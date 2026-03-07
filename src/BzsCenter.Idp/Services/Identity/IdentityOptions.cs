using BzsCenter.Shared.Infrastructure.Authorization;

namespace BzsCenter.Idp.Services.Identity;

public sealed class IdentitySeedOptions
{
    public SeedAdminOptions Admin { get; init; } = new();
    public string[] InitialRoles { get; init; } = [IdentitySeedConstants.UserRoleName];

    public Dictionary<string, string[]> RolePermissions { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [IdentitySeedConstants.UserRoleName] = [PermissionConstants.UsersReadSelf],
        };

    public Dictionary<string, string[]> PermissionScopes { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [PermissionConstants.UsersReadSelf] = [PermissionConstants.ScopeApi],
            [PermissionConstants.UsersReadAll] = [PermissionConstants.ScopeApi],
            [PermissionConstants.UsersWrite] = [PermissionConstants.ScopeApi],
            [PermissionConstants.RolesRead] = [PermissionConstants.ScopeApi],
            [PermissionConstants.RolesWrite] = [PermissionConstants.ScopeApi],
            [PermissionConstants.ClientsRead] = [PermissionConstants.ScopeApi],
            [PermissionConstants.ClientsWrite] = [PermissionConstants.ScopeApi],
        };

    public string[] AdditionalScopes { get; init; } = [PermissionConstants.ScopeApi];
}

public sealed class SeedAdminOptions
{
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
