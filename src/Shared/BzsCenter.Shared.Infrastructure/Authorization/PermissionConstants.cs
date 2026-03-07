namespace BzsCenter.Shared.Infrastructure.Authorization;

public static class PermissionConstants
{
    public const string ClaimType = "permission";

    public const string UsersReadSelf = "users.read.self";
    public const string UsersReadAll = "users.read.all";
    public const string UsersWrite = "users.write";
    public const string RolesRead = "roles.read";
    public const string RolesWrite = "roles.write";
    public const string ClientsRead = "clients.read";
    public const string ClientsWrite = "clients.write";

    public const string ScopeApi = "api";
}
