using System.Text.RegularExpressions;
using BzsCenter.Idp.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace BzsCenter.Idp.E2ETests;

[Collection(E2ETestCollection.Name)]
public sealed class AuthExperienceE2ETests(AppHostFixture fixture) : PageTest
{
    [Fact]
    public async Task LoginPage_AllowsThemeAndLanguageSwitching()
    {
        await Page.GotoAsync(fixture.BuildUrl("/login"));
        await Expect(Page.Locator("#username")).ToBeVisibleAsync();

        await AppUi.OpenPreferencesAsync(this);
        await Page.GetByRole(AriaRole.Menuitemradio, new() { Name = "EN" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("Welcome back", RegexOptions.IgnoreCase) })).ToBeVisibleAsync();

        await AppUi.OpenPreferencesAsync(this);
        await Page.GetByRole(AriaRole.Menuitemradio, new() { NameRegex = new Regex("Light|浅色", RegexOptions.IgnoreCase) }).ClickAsync();

        var theme = await Page.EvaluateAsync<string>("() => document.documentElement.getAttribute('data-theme') || ''");
        Assert.Equal("light", theme);
    }

    [Fact]
    public async Task PublicPages_RenderExpectedServerOwnedShells()
    {
        await Page.GotoAsync(fixture.BuildUrl("/login"));
        await Expect(Page.GetByRole(AriaRole.Heading)).ToContainTextAsync(new Regex("欢迎回来|Welcome back", RegexOptions.IgnoreCase));

        await Page.GotoAsync(fixture.BuildUrl("/logout?returnUrl=%2F"));
        await Expect(Page.GetByRole(AriaRole.Heading)).ToContainTextAsync(new Regex("退出|sign out", RegexOptions.IgnoreCase));

        await Page.GotoAsync(fixture.BuildUrl("/account/denied"));
        await Expect(Page.GetByRole(AriaRole.Heading)).ToContainTextAsync(new Regex("拒绝|denied|access", RegexOptions.IgnoreCase));

        await Page.GotoAsync(fixture.BuildUrl("/not-found"));
        await Expect(Page.GetByRole(AriaRole.Heading)).ToContainTextAsync(new Regex("不存在|not found", RegexOptions.IgnoreCase));
    }
}
