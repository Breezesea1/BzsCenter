using BzsOIDC.Idp.Services.Admin;
using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Idp.Services.Oidc;
using BzsOIDC.Idp.UnitTests.TestDoubles;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Services.Admin;

public sealed class AdminDashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsAggregatedCounts()
    {
        var userService = Substitute.For<IUserService>();
        userService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([
                CreateUser(Guid.Parse("00000000-0000-0000-0000-000000000001"), "admin-01", "admin01@example.com"),
                CreateUser(Guid.Parse("00000000-0000-0000-0000-000000000002"), "user-02", "user02@example.com"),
                CreateUser(Guid.Parse("00000000-0000-0000-0000-000000000003"), "user-03", "user03@example.com"),
            ]);

        var clientService = Substitute.For<IOidcClientService>();
        clientService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([
                new OidcClientResponse
                {
                    ClientId = "web-portal",
                    DisplayName = "Web Portal",
                    Profile = OidcClientProfile.FirstPartyInteractive,
                    Scopes = ["api"]
                },
                new OidcClientResponse
                {
                    ClientId = "jobs-daemon",
                    DisplayName = "Jobs Daemon",
                    Profile = OidcClientProfile.FirstPartyMachine,
                    Scopes = ["api", "jobs"]
                }
            ]);

        var permissionScopeService = Substitute.For<IPermissionScopeService>();
        permissionScopeService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([
                new PermissionScopeResponse
                {
                    Permission = "clients.read",
                    Scopes = ["api"]
                },
                new PermissionScopeResponse
                {
                    Permission = "clients.write",
                    Scopes = ["api", "jobs"]
                }
            ]);

        var userManager = new TestUserManager((user, role) =>
            user.UserName == "admin-01" &&
            string.Equals(role, IdentitySeedConstants.AdminRoleName, StringComparison.Ordinal));

        var sut = new AdminDashboardService(userService, clientService, permissionScopeService, userManager);

        var result = await sut.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(3, result.TotalUsers);
        Assert.Equal(1, result.AdminUsers);
        Assert.Equal(2, result.StandardUsers);
        Assert.Equal(2, result.TotalClients);
        Assert.Equal(1, result.InteractiveClients);
        Assert.Equal(1, result.MachineClients);
        Assert.Equal(2, result.TotalPermissionMappings);
        Assert.Equal(3, result.TotalConfiguredScopes);
    }

    private static BzsOIDC.Idp.Models.BzsUser CreateUser(Guid id, string userName, string email)
    {
        return new BzsOIDC.Idp.Models.BzsUser
        {
            Id = id,
            UserName = userName,
            Email = email
        };
    }
}
