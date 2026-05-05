using System.Security.Claims;
using Bunit;
using BzsOIDC.Idp.Components.Admin;
using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Components.Admin;

public sealed class RoleManagementPageTests
{
    [Fact]
    public void Render_WhenAdminHasData_ShowsRolesAndGroupedPermissions()
    {
        using var context = CreateContext();
        var adminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var operatorRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var roleService = Substitute.For<IRoleManagementService>();
        roleService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([
                new RoleResponse { Id = adminRoleId, Name = "admin", IsProtected = true, PermissionCount = 1 },
                new RoleResponse { Id = operatorRoleId, Name = "operators", PermissionCount = 0 },
            ]);
        roleService.GetByIdAsync(adminRoleId, Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = adminRoleId,
                Name = "admin",
                IsProtected = true,
                PermissionCount = 1,
                Permissions = ["users.read.all"],
            });

        var catalogService = Substitute.For<IPermissionCatalogService>();
        catalogService.GetResourcesAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ProtectedResourceResponse
                {
                    Key = "admin-api",
                    DisplayName = "Admin API",
                    Permissions =
                    [
                        new PermissionDefinitionResponse
                        {
                            ResourceKey = "admin-api",
                            Name = "users.read.all",
                            DisplayName = "Read users",
                            IsActive = true,
                        },
                        new PermissionDefinitionResponse
                        {
                            ResourceKey = "admin-api",
                            Name = "roles.write",
                            DisplayName = "Write roles",
                            IsActive = true,
                        },
                    ],
                },
            ]);

        context.Services.AddSingleton(roleService);
        context.Services.AddSingleton(catalogService);
        context.Services.AddSingleton<IStringLocalizer<RoleManagement>, TestStringLocalizer<RoleManagement>>();
        context.Services.AddSingleton<IHttpContextAccessor>(CreateAdminHttpContextAccessor());

        var cut = context.Render<RoleManagement>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("admin", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("operators", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Admin API", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("users.read.all", cut.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll("#selected-role-name[disabled]"));
        });
    }

    [Fact]
    public void Render_WhenRoleSearchChanges_FiltersRoleList()
    {
        using var context = CreateContext();
        var adminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var roleService = Substitute.For<IRoleManagementService>();
        roleService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([
                new RoleResponse { Id = adminRoleId, Name = "admin", IsProtected = true },
                new RoleResponse { Id = Guid.NewGuid(), Name = "operators" },
            ]);
        roleService.GetByIdAsync(adminRoleId, Arg.Any<CancellationToken>())
            .Returns(new RoleResponse { Id = adminRoleId, Name = "admin", IsProtected = true });

        var catalogService = Substitute.For<IPermissionCatalogService>();
        catalogService.GetResourcesAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        context.Services.AddSingleton(roleService);
        context.Services.AddSingleton(catalogService);
        context.Services.AddSingleton<IStringLocalizer<RoleManagement>, TestStringLocalizer<RoleManagement>>();
        context.Services.AddSingleton<IHttpContextAccessor>(CreateAdminHttpContextAccessor());

        var cut = context.Render<RoleManagement>();
        cut.WaitForAssertion(() => Assert.Contains("operators", cut.Markup, StringComparison.Ordinal));

        cut.Find("#role-search").Input("oper");

        Assert.Contains("operators", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">admin</strong>", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SavePermissions_WhenSelectedPermission_CallsRoleManagementServiceWithSnapshot()
    {
        using var context = CreateContext();
        var roleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var roleService = Substitute.For<IRoleManagementService>();
        roleService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([new RoleResponse { Id = roleId, Name = "operators" }]);
        roleService.GetByIdAsync(roleId, Arg.Any<CancellationToken>())
            .Returns(new RoleResponse { Id = roleId, Name = "operators", Permissions = [] });
        roleService.SyncPermissionsAsync(roleId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(RoleManagementCommandResult<IReadOnlyList<string>>.Success(["roles.write"]));

        var catalogService = Substitute.For<IPermissionCatalogService>();
        catalogService.GetResourcesAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ProtectedResourceResponse
            {
                Key = "admin-api",
                DisplayName = "Admin API",
                Permissions =
                    [
                        new PermissionDefinitionResponse
                        {
                            ResourceKey = "admin-api",
                            Name = "roles.write",
                            DisplayName = "Write roles",
                            IsActive = true,
                        },
                    ],
                },
            ]);

        IReadOnlyList<string>? capturedPermissions = null;
        roleService.SyncPermissionsAsync(roleId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedPermissions = callInfo.ArgAt<IEnumerable<string>>(1).ToArray();
                return RoleManagementCommandResult<IReadOnlyList<string>>.Success(["roles.write"]);
            });

        context.Services.AddSingleton(roleService);
        context.Services.AddSingleton(catalogService);
        context.Services.AddSingleton<IStringLocalizer<RoleManagement>, TestStringLocalizer<RoleManagement>>();
        context.Services.AddSingleton<IHttpContextAccessor>(CreateAdminHttpContextAccessor());

        var cut = context.Render<RoleManagement>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input[type='checkbox']")));

        cut.Find("input[type='checkbox']").Change(true);
        cut.FindAll("button").Single(button => button.TextContent.Contains("SavePermissions", StringComparison.Ordinal)).Click();

        Assert.NotNull(capturedPermissions);
        Assert.Equal(["roles.write"], capturedPermissions);
        roleService.Received(1).SyncPermissionsAsync(roleId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    private static IHttpContextAccessor CreateAdminHttpContextAccessor()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "admin"),
            ],
            "TestAuth");

        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
    }
}
