using BzsOIDC.Idp.Models;
using BzsOIDC.Idp.Services.Identity;

namespace BzsOIDC.Idp.UnitTests.Services.Identity;

public sealed class RoleManagementPolicyTests
{
    [Fact]
    public void ValidateCreate_WhenRoleNameBlank_ReturnsValidationFailure()
    {
        var sut = new RoleManagementPolicy();

        var errors = sut.ValidateCreate(" ", reservedRoleExists: false);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateCreate_WhenReservedAdminExists_ReturnsValidationFailure()
    {
        var sut = new RoleManagementPolicy();

        var errors = sut.ValidateCreate("admin", reservedRoleExists: true);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateCreate_WhenNonAdminRole_ReturnsNoErrors()
    {
        var sut = new RoleManagementPolicy();

        var errors = sut.ValidateCreate("operators", reservedRoleExists: true);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRename_WhenProtectedAdminRenamedAway_ReturnsValidationFailure()
    {
        var sut = new RoleManagementPolicy();
        var role = new BzsRole { Name = IdentitySeedConstants.AdminRoleName };

        var errors = sut.ValidateRename(role, "operators");

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateRename_WhenNonAdminRenamedToAdmin_ReturnsValidationFailure()
    {
        var sut = new RoleManagementPolicy();
        var role = new BzsRole { Name = "operators" };

        var errors = sut.ValidateRename(role, "admin");

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateRename_WhenNonAdminRenamedToNonAdmin_ReturnsNoErrors()
    {
        var sut = new RoleManagementPolicy();
        var role = new BzsRole { Name = "operators" };

        var errors = sut.ValidateRename(role, "support");

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateDelete_WhenProtectedAdmin_ReturnsValidationFailure()
    {
        var sut = new RoleManagementPolicy();
        var role = new BzsRole { Name = IdentitySeedConstants.AdminRoleName };

        var errors = sut.ValidateDelete(role);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateDelete_WhenNonAdminRole_ReturnsNoErrors()
    {
        var sut = new RoleManagementPolicy();
        var role = new BzsRole { Name = "operators" };

        var errors = sut.ValidateDelete(role);

        Assert.Empty(errors);
    }
}
