using BzsCenter.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BzsCenter.Idp.Services.Authorization;

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// 执行HandleRequirementAsync。
    /// </summary>
    /// <param name="context">参数context。</param>
    /// <param name="requirement">参数requirement。</param>
    /// <returns>执行结果。</returns>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var hasPermission = context.User.Claims.Any(c =>
            string.Equals(c.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
