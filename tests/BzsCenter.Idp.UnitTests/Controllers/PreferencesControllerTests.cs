using BzsCenter.Idp.Controllers;
using BzsCenter.Idp.Infra.Preferences;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace BzsCenter.Idp.UnitTests.Controllers;

public sealed class PreferencesControllerTests
{
    [Fact]
    public void SetCulture_WhenCultureSupported_AppendsCultureCookieAndRedirectsLocal()
    {
        var sut = CreateSut(static url => url == "/dashboard");

        var result = sut.SetCulture("en-US", "/dashboard");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);

        var setCookie = Assert.Single(sut.Response.Headers.SetCookie);
        Assert.Contains(".AspNetCore.Culture", setCookie, StringComparison.Ordinal);
        Assert.Contains("c%3Den-US%7Cuic%3Den-US", setCookie, StringComparison.Ordinal);
    }

    [Fact]
    public void SetCulture_WhenCultureInvalid_AppendsDefaultCultureCookieAndRedirectsRoot()
    {
        var sut = CreateSut(static _ => false);

        var result = sut.SetCulture("ja-JP", "https://evil.example");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirect.Url);

        var setCookie = Assert.Single(sut.Response.Headers.SetCookie);
        Assert.Contains("c%3Dzh-CN%7Cuic%3Dzh-CN", setCookie, StringComparison.Ordinal);
    }

    [Fact]
    public void SetTheme_WhenThemeSupported_AppendsThemeCookieAndRedirectsLocal()
    {
        var sut = CreateSut(static url => url == "/settings");

        var result = sut.SetTheme("dark", "/settings");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/settings", redirect.Url);

        var setCookie = Assert.Single(sut.Response.Headers.SetCookie);
        Assert.Contains($"{UiPreferences.ThemeCookieName}=dark", setCookie, StringComparison.Ordinal);
    }

    [Fact]
    public void SetTheme_WhenThemeInvalid_AppendsDefaultThemeCookieAndRedirectsRoot()
    {
        var sut = CreateSut(static _ => false);

        var result = sut.SetTheme("sepia", "https://evil.example");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirect.Url);

        var setCookie = Assert.Single(sut.Response.Headers.SetCookie);
        Assert.Contains($"{UiPreferences.ThemeCookieName}={UiPreferences.DefaultTheme}", setCookie, StringComparison.Ordinal);
    }

    private static PreferencesController CreateSut(Func<string, bool> isLocalUrl)
    {
        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>())
            .Returns(callInfo => isLocalUrl(callInfo.Arg<string>()));

        return new PreferencesController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
            Url = urlHelper,
        };
    }
}
