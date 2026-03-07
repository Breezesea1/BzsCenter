using Microsoft.AspNetCore.Authorization;

namespace BzsCenter.Idp.Services.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
