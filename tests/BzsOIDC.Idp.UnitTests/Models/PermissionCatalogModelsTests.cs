using BzsOIDC.Idp.Models;

namespace BzsOIDC.Idp.UnitTests.Models;

public sealed class PermissionCatalogModelsTests
{
    [Fact]
    public void ProtectedResource_Create_WhenKeyEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ProtectedResource.Create(" "));
    }

    [Fact]
    public void PermissionDefinition_Create_WhenNameEmpty_ThrowsArgumentException()
    {
        var resource = ProtectedResource.Create("orders-api");

        Assert.Throws<ArgumentException>(() => resource.AddPermission(" "));
    }

    [Fact]
    public void PermissionDefinition_Create_NormalizesNameToLowerInvariant()
    {
        var resource = ProtectedResource.Create("Orders-API");
        var permission = resource.AddPermission(" Orders.Read ");

        Assert.Equal("orders-api", resource.Key);
        Assert.Equal("orders.read", permission.Name);
    }

    [Fact]
    public void ProtectedResource_AddPermission_WhenDuplicateName_ThrowsInvalidOperationException()
    {
        var resource = ProtectedResource.Create("orders-api");
        resource.AddPermission("orders.read");

        Assert.Throws<InvalidOperationException>(() => resource.AddPermission(" Orders.Read "));
    }

    [Fact]
    public void PermissionDefinition_SyncReleaseScopes_WhenScopesEmpty_ThrowsArgumentException()
    {
        var resource = ProtectedResource.Create("orders-api");
        var permission = resource.AddPermission("orders.read");

        Assert.Throws<ArgumentException>(() => permission.SyncReleaseScopes([]));
    }
}
