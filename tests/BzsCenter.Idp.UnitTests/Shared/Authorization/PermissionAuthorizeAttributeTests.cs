using BzsCenter.Shared.Infrastructure.Authorization;

namespace BzsCenter.Idp.UnitTests.Shared.Authorization;

public class PermissionAuthorizeAttributeTests
{
    [Fact]
    public void Constructor_BuildsPolicyWithDefaultPrefix_AndTrimsPermission()
    {
        var attribute = new PermissionAuthorizeAttribute($"  {PermissionConstants.ClientsWrite}  ");

        Assert.Equal($"{PermissionAuthorizeAttribute.DefaultPolicyPrefix}{PermissionConstants.ClientsWrite}", attribute.Policy);
    }
}
