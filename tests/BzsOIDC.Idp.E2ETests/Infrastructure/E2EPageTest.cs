using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace BzsOIDC.Idp.E2ETests.Infrastructure;

public abstract class E2EPageTest : PageTest
{
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.IgnoreHTTPSErrors = true;
        return options;
    }
}
