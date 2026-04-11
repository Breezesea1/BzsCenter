using BzsOIDC.Idp.Controllers;
using BzsOIDC.Idp.Services.Admin;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Controllers;

public sealed class AdminDashboardControllerTests
{
    [Fact]
    public async Task GetSummary_ReturnsOkPayload()
    {
        var service = Substitute.For<IAdminDashboardService>();
        service.GetSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminDashboardSummaryResponse
            {
                TotalUsers = 12,
                AdminUsers = 3,
                StandardUsers = 9,
                TotalClients = 5,
                InteractiveClients = 3,
                MachineClients = 2,
                TotalPermissionMappings = 8,
                TotalConfiguredScopes = 11
            });

        var sut = new AdminDashboardController(service);

        var result = await sut.GetSummary(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminDashboardSummaryResponse>(ok.Value);
        Assert.Equal(12, payload.TotalUsers);
        Assert.Equal(5, payload.TotalClients);
    }
}
