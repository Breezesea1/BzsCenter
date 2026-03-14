using BzsCenter.Idp.Client.Services.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace BzsCenter.Idp.UnitTests.Services.Dashboard;

public sealed class DashboardServiceExtensionsTests
{
    [Fact]
    public async Task AddAdminDashboardClient_RegistersDashboardClient()
    {
        var services = new ServiceCollection();
        services.AddAdminDashboardClient(_ => new Uri("https://localhost:5001/"));

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var dashboardClient = scope.ServiceProvider.GetRequiredService<IAdminDashboardClient>();

        Assert.NotNull(dashboardClient);
    }
}
