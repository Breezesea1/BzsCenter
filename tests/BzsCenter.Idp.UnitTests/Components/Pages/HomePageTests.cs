using System.Security.Claims;
using ApexCharts;
using Bunit;
using BzsCenter.Idp.Client.Components.Pages;
using BzsCenter.Idp.Client.Services.Dashboard;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "admin"),
            ], "TestAuth"))));
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

    [Fact]
    public void Home_WhenAnonymous_RedirectsToLogin()
    {
        using var context = new BunitContext();
        context.Services.AddApexCharts();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var dashboardClient = Substitute.For<IAdminDashboardClient>();
        context.Services.AddSingleton(dashboardClient);
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity())));
        context.Services.AddSingleton<IStringLocalizer<Home>, TestDoubles.TestStringLocalizer<Home>>();
        context.Services.AddSingleton<IStringLocalizer<Dashboard>, TestDoubles.TestStringLocalizer<Dashboard>>();

        var navigationManager = context.Services.GetRequiredService<NavigationManager>();

        context.Render<Home>();

        Assert.Equal("http://localhost/login?returnUrl=%2F", navigationManager.Uri);
        dashboardClient.DidNotReceive().GetSummaryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Home_WhenAuthenticatedWithoutAdminRole_ShowsAccessLimitedWithoutCallingDashboardApi()
    {
        using var context = new BunitContext();
        context.Services.AddApexCharts();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var dashboardClient = Substitute.For<IAdminDashboardClient>();
        context.Services.AddSingleton(dashboardClient);
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "github-user"),
                new Claim(ClaimTypes.Email, "github-user@example.com"),
                new Claim(ClaimTypes.Role, "user"),
            ], "TestAuth"))));
        context.Services.AddSingleton<IStringLocalizer<Home>, TestDoubles.TestStringLocalizer<Home>>();
        context.Services.AddSingleton<IStringLocalizer<Dashboard>, TestDoubles.TestStringLocalizer<Dashboard>>();

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("AccessLimitedTitle", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("TotalUsers", cut.Markup, StringComparison.Ordinal);
        });

        dashboardClient.DidNotReceive().GetSummaryAsync(Arg.Any<CancellationToken>());
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state = new(user);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(_state);
        }
    }
}
