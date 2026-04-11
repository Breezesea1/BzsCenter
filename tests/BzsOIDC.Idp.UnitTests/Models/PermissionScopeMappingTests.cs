using BzsOIDC.Idp.Models;

namespace BzsOIDC.Idp.UnitTests.Models;

public sealed class PermissionScopeMappingTests
{
    [Fact]
    public void Create_WhenValuesValid_NormalizesPermissionAndScope()
    {
        var mapping = PermissionScopeMapping.Create(" Users.Write ", " API ");

        Assert.Equal("users.write", mapping.Permission);
        Assert.Equal("api", mapping.Scope);
    }

    [Fact]
    public void Create_WhenPermissionBlank_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => PermissionScopeMapping.Create(" ", "api"));

        Assert.Equal("permission", exception.ParamName);
    }

    [Fact]
    public void Create_WhenScopeBlank_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => PermissionScopeMapping.Create("users.write", " "));

        Assert.Equal("scope", exception.ParamName);
    }
}
