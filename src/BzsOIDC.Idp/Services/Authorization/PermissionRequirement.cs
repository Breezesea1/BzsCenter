using Microsoft.AspNetCore.Authorization;

namespace BzsOIDC.Idp.Services.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
