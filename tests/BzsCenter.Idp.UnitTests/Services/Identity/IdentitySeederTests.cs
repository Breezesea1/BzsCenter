using BzsCenter.Idp.Models;
using BzsCenter.Idp.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BzsCenter.Idp.UnitTests.Services.Identity;

public sealed class IdentitySeederTests
{
    [Fact]
    public async Task SeedAsync_WhenAdminUserNameMissing_ThrowsInvalidOperationException()
    {
        var roleService = Substitute.For<IRoleService>();
        var rolePermissionService = Substitute.For<IRolePermissionService>();
        var permissionScopeService = Substitute.For<IPermissionScopeService>();
        var userService = Substitute.For<IUserService>();

        var options = Options.Create(new IdentitySeedOptions
        {
            Admin = new SeedAdminOptions
            {
                UserName = string.Empty,
                Password = "Passw0rd!",
            },
        });

        var sut = new IdentitySeeder(
            roleService,
            rolePermissionService,
            permissionScopeService,
            userService,
            options,
            NullLogger<IdentitySeeder>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SeedAsync());
    }

    [Fact]
    public async Task SeedAsync_WhenAdminAlreadyExists_DoesNotCreateUserAgain()
    {
        var roleService = Substitute.For<IRoleService>();
        var rolePermissionService = Substitute.For<IRolePermissionService>();
        var permissionScopeService = Substitute.For<IPermissionScopeService>();
        var userService = Substitute.For<IUserService>();

        roleService.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BzsRole { Id = Guid.NewGuid(), Name = IdentitySeedConstants.AdminRoleName });
        rolePermissionService.SyncPermissionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(IdentityResult.Success));

        var existingAdmin = new BzsUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin",
        };

        userService.GetByNameAsync("admin", Arg.Any<CancellationToken>())
            .Returns(existingAdmin);
        userService.IsInRoleAsync(default, default!, default)
            .ReturnsForAnyArgs(Task.FromResult(true));

        var options = Options.Create(new IdentitySeedOptions
        {
            Admin = new SeedAdminOptions
            {
                UserName = "admin",
                Password = "Passw0rd!",
            },
            InitialRoles = [IdentitySeedConstants.UserRoleName],
            RolePermissions = new Dictionary<string, string[]>
            {
                [IdentitySeedConstants.UserRoleName] = ["users.read.self"],
            },
            PermissionScopes = new Dictionary<string, string[]>
            {
                ["users.read.self"] = ["api"],
            },
        });

        var sut = new IdentitySeeder(
            roleService,
            rolePermissionService,
            permissionScopeService,
            userService,
            options,
            NullLogger<IdentitySeeder>.Instance);

        await sut.SeedAsync();

        await permissionScopeService.Received(1)
            .InitializeDefaultsIfEmptyAsync(options.Value.PermissionScopes, Arg.Any<CancellationToken>());
        await userService.DidNotReceive()
            .CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await userService.DidNotReceive()
            .AddToRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_WhenAdminMissing_CreatesUserAndAssignsAdminRole()
    {
        var roleService = Substitute.For<IRoleService>();
        var rolePermissionService = Substitute.For<IRolePermissionService>();
        var permissionScopeService = Substitute.For<IPermissionScopeService>();
        var userService = Substitute.For<IUserService>();

        roleService.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BzsRole { Id = Guid.NewGuid(), Name = IdentitySeedConstants.AdminRoleName });
        rolePermissionService.SyncPermissionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(IdentityResult.Success));

        var createdAdmin = new BzsUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin",
        };

        userService.GetByNameAsync("admin", Arg.Any<CancellationToken>())
            .Returns((BzsUser?)null, createdAdmin);
        userService.CreateAsync("admin", "Passw0rd!", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(IdentityResult.Success));
        userService.IsInRoleAsync(default, default!, default)
            .ReturnsForAnyArgs(Task.FromResult(false));
        userService.AddToRoleAsync(default, default!, default)
            .ReturnsForAnyArgs(Task.FromResult(IdentityResult.Success));

        var options = Options.Create(new IdentitySeedOptions
        {
            Admin = new SeedAdminOptions
            {
                UserName = "admin",
                Password = "Passw0rd!",
            },
            InitialRoles = [IdentitySeedConstants.UserRoleName],
            RolePermissions = new Dictionary<string, string[]>
            {
                [IdentitySeedConstants.UserRoleName] = ["users.read.self"],
            },
            PermissionScopes = new Dictionary<string, string[]>
            {
                ["users.read.self"] = ["api"],
            },
        });

        var sut = new IdentitySeeder(
            roleService,
            rolePermissionService,
            permissionScopeService,
            userService,
            options,
            NullLogger<IdentitySeeder>.Instance);

        await sut.SeedAsync();

        await userService.Received(1)
            .CreateAsync("admin", "Passw0rd!", Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await userService.Received(1)
            .AddToRoleAsync(createdAdmin.Id, IdentitySeedConstants.AdminRoleName, Arg.Any<CancellationToken>());
    }
}
