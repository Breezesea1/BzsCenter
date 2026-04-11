using BzsOIDC.Idp.Infra.Preferences;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace BzsOIDC.Idp.Controllers;

[Route("preferences")]
public sealed class PreferencesController : Controller
{
    [HttpGet("set-culture")]
    [HttpGet("~/account/set-culture")]
    public IActionResult SetCulture([FromQuery] string? culture, [FromQuery] string? returnUrl)
    {
        var normalizedCulture = UiPreferences.NormalizeCulture(culture);

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedCulture)),
            UiPreferences.CreateCookieOptions());

        return RedirectToSafeLocal(returnUrl);
    }

    [HttpGet("set-theme")]
    public IActionResult SetTheme([FromQuery] string? theme, [FromQuery] string? returnUrl)
    {
        var normalizedTheme = UiPreferences.NormalizeTheme(theme);

        Response.Cookies.Append(
            UiPreferences.ThemeCookieName,
            normalizedTheme,
            UiPreferences.CreateCookieOptions());

        return RedirectToSafeLocal(returnUrl);
    }

    private IActionResult RedirectToSafeLocal(string? returnUrl)
    {
        if (IsSafeLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl!);
        }

        return Redirect("/");
    }

    private bool IsSafeLocalUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl);
    }
}
