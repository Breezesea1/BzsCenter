using ApexCharts;
using Bunit;
using BzsCenter.Idp.Client.Components.Pages;
using BzsCenter.Idp.Client.Services.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BzsCenter.Idp.UnitTests.Components.Pages;

public sealed class HomePageTests
{
    [Fact]
    public void Home_RendersDashboardSummaryCards()
    {
        using var context = new BunitContext();
        context.Services.AddApexCharts();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var dashboardClient = Substitute.For<IAdminDashboardClient>();
        dashboardClient.GetSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminDashboardSummaryModel
            {
                TotalUsers = 18,
                AdminUsers = 4,
                StandardUsers = 14,
                TotalClients = 6,
                InteractiveClients = 4,
                MachineClients = 2,
                TotalPermissionMappings = 12,
                TotalConfiguredScopes = 20
            });

        context.Services.AddSingleton(dashboardClient);
        context.Services.AddSingleton<IStringLocalizer<Home>, TestDoubles.TestStringLocalizer<Home>>();
        context.Services.AddSingleton<IStringLocalizer<Dashboard>, TestDoubles.TestStringLocalizer<Dashboard>>();

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("18", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("6", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("TotalUsers", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("TotalClients", cut.Markup, StringComparison.Ordinal);
        });
    }
}
