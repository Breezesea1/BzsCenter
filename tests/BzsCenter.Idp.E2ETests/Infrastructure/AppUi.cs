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
