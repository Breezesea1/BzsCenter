using System.Security.Claims;
using BzsOIDC.Idp.Services.Authorization;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BzsOIDC.Idp.UnitTests.Services.Authorization;

public class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenPermissionClaimExists()
    {
        var requirement = new PermissionRequirement(PermissionConstants.UsersWrite);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionConstants.ClaimType, PermissionConstants.UsersWrite),
        ], "TestAuth"));

        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        var handler = new PermissionAuthorizationHandler();

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_Fails_WhenPermissionClaimMissing()
    {
        var requirement = new PermissionRequirement(PermissionConstants.UsersWrite);
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"));

        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        var handler = new PermissionAuthorizationHandler();

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
