using System.Text.RegularExpressions;
using BzsCenter.Idp.E2ETests.Infrastructure;
using Microsoft.Playwright;

namespace BzsCenter.Idp.E2ETests;

[Collection(E2ETestCollection.Name)]
public sealed class AuthExperienceE2ETests(AppHostFixture fixture) : E2EPageTest
{
    [Fact]
    public async Task LoginPage_AllowsThemeAndLanguageSwitching()
    {
        await Page.GotoAsync(fixture.BuildUrl("/login"));
        await Expect(Page.Locator("#username")).ToBeVisibleAsync();

        await AppUi.OpenPreferencesAsync(this);
        await Page.GetByRole(AriaRole.Menuitemradio, new() { Name = "EN" }).ClickAsync();
        await AppUi.WaitForAppReadyAsync(this);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("Welcome back", RegexOptions.IgnoreCase) })).ToBeVisibleAsync();

        await AppUi.OpenPreferencesAsync(this);
        await Page.GetByRole(AriaRole.Menuitemradio, new() { NameRegex = new Regex("Light|浅色", RegexOptions.IgnoreCase) }).ClickAsync();
        await AppUi.WaitForAppReadyAsync(this);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
    }

    [Fact]
    public async Task PublicPages_RenderExpectedServerOwnedShells()
    {
        await Page.GotoAsync(fixture.BuildUrl("/login"));
        await Expect(Page.GetByRole(AriaRole.Heading)).ToContainTextAsync(new Regex("欢迎回来|Welcome back", RegexOptions.IgnoreCase));

        await Page.GotoAsync(fixture.BuildUrl("/logout?returnUrl=%2F"));
        await Expect(Page.GetByRole(AriaRole.Heading)).ToContainTextAsync(new Regex("退出|sign out", RegexOptions.IgnoreCase));

        await Page.GotoAsync(fixture.BuildUrl("/account/denied"));
        await Expect(Page.Locator(".denied-page")).ToBeVisibleAsync();
        await Expect(Page.Locator(".denied-secondary")).ToHaveAttributeAsync("href", "/login");

        await Page.GotoAsync(fixture.BuildUrl("/not-found"));
        await Expect(Page.GetByRole(AriaRole.Heading)).ToContainTextAsync(new Regex("不存在|not found", RegexOptions.IgnoreCase));
    }

    [Fact]
    public async Task LogoutFlow_AfterAdminSession_RedirectsProtectedRouteBackToLogin()
    {
        await AppUi.LoginAsAdminAsync(this, fixture, "/admin/users");
        await Expect(Page).ToHaveURLAsync(new Regex("/admin/users", RegexOptions.IgnoreCase), new() { Timeout = 30000 });
        await Page.GotoAsync(fixture.BuildUrl("/admin/users"));
        await Page.Locator(".admin-table").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20000 });

        await AppUi.LogoutAsync(this, fixture, "/admin/users");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/login\?returnUrl=%2Fadmin%2Fusers", RegexOptions.IgnoreCase));
        await Expect(Page.Locator("#username")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_WhenAlreadyAuthenticated_RedirectsToHome()
    {
        await AppUi.LoginAsAdminAsync(this, fixture);

        await Page.GotoAsync(fixture.BuildUrl("/login"));

        await Page.WaitForURLAsync(new Regex(@"/$"));
        await Expect(Page.Locator("#username")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SidebarAvatar_WhenClicked_OpensUserMenu()
    {
        await AppUi.LoginAsAdminAsync(this, fixture);
        await Page.GotoAsync(fixture.BuildUrl("/"));

        await AppUi.OpenSidebarUserMenuAsync(this);

        await Expect(Page.Locator(".sidebar-user-panel__action")).ToContainTextAsync(new Regex("退出|log out", RegexOptions.IgnoreCase));
    }
}
