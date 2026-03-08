namespace BzsCenter.Idp.Infra.Preferences;

internal static class UiPreferences
{
    internal const string DefaultCulture = "zh-CN";
    internal const string DefaultTheme = "system";
    internal const string ThemeCookieName = "bzs-theme";

    internal static readonly string[] SupportedCultureNames =
    [
        "zh-CN",
        "en-US",
    ];

    internal static readonly string[] SupportedThemeNames =
    [
        "light",
        "dark",
        "system",
    ];

    internal static CookieOptions CreateCookieOptions()
    {
        return new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = false,
            Secure = true,
            Path = "/",
        };
    }

    internal static string NormalizeCulture(string? culture)
    {
        return Normalize(culture, SupportedCultureNames, DefaultCulture);
    }

    internal static string NormalizeTheme(string? theme)
    {
        return Normalize(theme, SupportedThemeNames, DefaultTheme);
    }

    private static string Normalize(string? value, string[] supportedValues, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        foreach (var supportedValue in supportedValues)
        {
            if (string.Equals(supportedValue, value, StringComparison.OrdinalIgnoreCase))
            {
                return supportedValue;
            }
        }

        return defaultValue;
    }
}
