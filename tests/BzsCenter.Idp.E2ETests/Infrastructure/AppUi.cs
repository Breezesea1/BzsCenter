using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

internal static class AppUi
{
    public static async Task LoginAsAdminAsync(PageTest test, AppHostFixture fixture, string returnPath = "/")
    {
        await test.Page.GotoAsync(fixture.BuildUrl($"/testing/e2e/sign-in-admin?returnUrl={Uri.EscapeDataString(returnPath)}"));
        await test.Expect(test.Page.Locator("#username")).ToHaveCountAsync(0);
        await WaitForAppReadyAsync(test);
    }

    public static async Task LoginWithPasswordAsync(
        PageTest test,
        AppHostFixture fixture,
        string userName,
        string password,
        string returnPath = "/",
        bool rememberMe = false)
    {
        await test.Page.GotoAsync(fixture.BuildUrl($"/login?returnUrl={Uri.EscapeDataString(returnPath)}"));
        await test.Expect(test.Page.Locator("#username")).ToBeVisibleAsync();

        await test.Page.Locator("#username").FillAsync(userName);
        await test.Page.Locator("#password").FillAsync(password);

        var rememberMeToggle = test.Page.Locator("input[name='RememberMe']");
        if (rememberMe)
        {
            await rememberMeToggle.CheckAsync();
        }

        await test.Page.Locator("form.login-form button[type='submit']").ClickAsync();
        await WaitForAppReadyAsync(test);
    }

    public static async Task LogoutAsync(PageTest test, AppHostFixture fixture, string returnPath = "/")
    {
        await test.Page.GotoAsync(fixture.BuildUrl($"/logout?returnUrl={Uri.EscapeDataString(returnPath)}"));
        await test.Expect(test.Page.Locator("form.logout-form button[type='submit']")).ToBeVisibleAsync();
        await test.Page.Locator("form.logout-form button[type='submit']").ClickAsync();
        await WaitForAppReadyAsync(test);
    }

    public static async Task WaitForAppReadyAsync(PageTest test)
    {
        await test.Expect(test.Page.Locator("#components-reconnect-modal")).ToBeHiddenAsync(new() { Timeout = 30000 });
    }

    public static async Task OpenPreferencesAsync(PageTest test)
    {
        var trigger = test.Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("偏好设置|Preferences", RegexOptions.IgnoreCase) });
        var menu = test.Page.GetByRole(AriaRole.Menu);

        await test.Expect(trigger).ToBeVisibleAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await trigger.ClickAsync();

            try
            {
                await test.Expect(menu).ToBeVisibleAsync(new() { Timeout = 3000 });
                return;
            }
            catch (PlaywrightException) when (attempt < 2)
            {
                await test.Page.WaitForTimeoutAsync(350);
            }
        }

        await test.Expect(menu).ToBeVisibleAsync();
    }

    public static string UniqueName(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 9, prefix.Length + 9)];
    }
}
