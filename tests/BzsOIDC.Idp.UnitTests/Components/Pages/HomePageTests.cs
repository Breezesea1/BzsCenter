using System.Security.Claims;
using ApexCharts;
using Bunit;
using BzsOIDC.Idp.Client.Components.Pages;
using BzsOIDC.Idp.Client.Services.Dashboard;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Components.Pages;

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
            .Returns(AdminDashboardSummaryResult.Success(CreateSummary()));

        context.Services.AddSingleton(dashboardClient);
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(CreateAdminPrincipal()));
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

    [Fact]
    public void Home_WhenDashboardSummaryIsUnavailable_ShowsUnavailableStateInsteadOfAccessLimited()
    {
        using var context = new BunitContext();
        context.Services.AddApexCharts();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var dashboardClient = Substitute.For<IAdminDashboardClient>();
        dashboardClient.GetSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(AdminDashboardSummaryResult.Unavailable());

        context.Services.AddSingleton(dashboardClient);
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(CreateAdminPrincipal()));
        context.Services.AddSingleton<IStringLocalizer<Home>, TestDoubles.TestStringLocalizer<Home>>();
        context.Services.AddSingleton<IStringLocalizer<Dashboard>, TestDoubles.TestStringLocalizer<Dashboard>>();

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("UnavailableTitle", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("AccessLimitedTitle", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Home_WhenAuthenticationStateBecomesAdminAfterInitialAnonymousState_LoadsDashboardSummary()
    {
        using var context = new BunitContext();
        context.Services.AddApexCharts();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var dashboardClient = Substitute.For<IAdminDashboardClient>();
        dashboardClient.GetSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(AdminDashboardSummaryResult.Success(CreateSummary()));

        var authStateProvider = new MutableAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity()));

        context.Services.AddSingleton(dashboardClient);
        context.Services.AddSingleton<AuthenticationStateProvider>(authStateProvider);
        context.Services.AddSingleton<IStringLocalizer<Home>, TestDoubles.TestStringLocalizer<Home>>();
        context.Services.AddSingleton<IStringLocalizer<Dashboard>, TestDoubles.TestStringLocalizer<Dashboard>>();

        var cut = context.Render<Home>();

        Assert.Equal("http://localhost/login?returnUrl=%2F", context.Services.GetRequiredService<NavigationManager>().Uri);

        authStateProvider.SetUser(CreateAdminPrincipal());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("18", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("TotalUsers", cut.Markup, StringComparison.Ordinal);
        });

        dashboardClient.Received(1).GetSummaryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Home_WhenAuthenticationRefreshesDuringSlowRequest_KeepsLatestDashboardState()
    {
        using var context = new BunitContext();
        context.Services.AddApexCharts();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var firstRequest = new TaskCompletionSource<AdminDashboardSummaryResult>();
        var secondRequest = new TaskCompletionSource<AdminDashboardSummaryResult>();
        var callCount = 0;

        var dashboardClient = Substitute.For<IAdminDashboardClient>();
        dashboardClient.GetSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1 ? firstRequest.Task : secondRequest.Task;
            });

        var authStateProvider = new MutableAuthenticationStateProvider(CreateAdminPrincipal());

        context.Services.AddSingleton(dashboardClient);
        context.Services.AddSingleton<AuthenticationStateProvider>(authStateProvider);
        context.Services.AddSingleton<IStringLocalizer<Home>, TestDoubles.TestStringLocalizer<Home>>();
        context.Services.AddSingleton<IStringLocalizer<Dashboard>, TestDoubles.TestStringLocalizer<Dashboard>>();

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("dashboard-skeleton-card", cut.Markup, StringComparison.Ordinal);
        });

        authStateProvider.SetUser(CreateAdminPrincipal());

        secondRequest.SetResult(AdminDashboardSummaryResult.Success(CreateSummary()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("18", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("UnavailableTitle", cut.Markup, StringComparison.Ordinal);
        });

        firstRequest.SetResult(AdminDashboardSummaryResult.Unavailable());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("18", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("UnavailableTitle", cut.Markup, StringComparison.Ordinal);
        });
    }

    private static ClaimsPrincipal CreateAdminPrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "admin"),
        ], "TestAuth"));
    }

    private static AdminDashboardSummaryModel CreateSummary()
    {
        return new AdminDashboardSummaryModel
        {
            TotalUsers = 18,
            AdminUsers = 4,
            StandardUsers = 14,
            TotalClients = 6,
            InteractiveClients = 4,
            MachineClients = 2,
            TotalPermissionMappings = 12,
            TotalConfiguredScopes = 20
        };
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state = new(user);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(_state);
        }
    }

    private sealed class MutableAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        private AuthenticationState _state = new(user);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(_state);
        }

        public void SetUser(ClaimsPrincipal user)
        {
            _state = new AuthenticationState(user);
            NotifyAuthenticationStateChanged(Task.FromResult(_state));
        }
    }
}
