using Microsoft.AspNetCore.Authorization;

namespace BzsCenter.Shared.Infrastructure.Authorization;

public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
{
    public const string DefaultPolicyPrefix = "perm:";

    public PermissionAuthorizeAttribute(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("Permission cannot be empty.", nameof(permission));
        }

        Policy = $"{DefaultPolicyPrefix}{permission.Trim()}";
    }
}
