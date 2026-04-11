using Microsoft.AspNetCore.WebUtilities;

namespace BzsOIDC.Idp.Services.Identity;

internal static class ExternalAuthenticationFailureResponseBuilder
{
    internal static string BuildLoginRedirectPath(Exception? failure, string? redirectUri)
    {
        var errorCode = ResolveErrorCode(failure);
        var returnUrl = ExtractSafeReturnUrl(redirectUri);

        var queryValues = new Dictionary<string, string?>
        {
            ["error"] = errorCode,
        };

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            queryValues["returnUrl"] = returnUrl;
        }

        return QueryHelpers.AddQueryString("/login", queryValues);
    }

    private static string ResolveErrorCode(Exception? failure)
    {
        var message = failure?.Message ?? string.Empty;

        if (message.Contains("Correlation failed", StringComparison.OrdinalIgnoreCase))
        {
            return "external_login_expired";
        }

        if (message.Contains("access_denied", StringComparison.OrdinalIgnoreCase))
        {
            return "external_login_access_denied";
        }

        return "external_login_failed";
    }

    private static string? ExtractSafeReturnUrl(string? redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return null;
        }

        var queryString = ExtractQueryString(redirectUri);
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(queryString);
        if (!query.TryGetValue("returnUrl", out var returnUrl))
        {
            return null;
        }

        var candidate = returnUrl.ToString();
        return IsSafeLocalUrl(candidate) ? candidate : null;
    }

    private static string? ExtractQueryString(string redirectUri)
    {
        var queryStartIndex = redirectUri.IndexOf('?');
        if (queryStartIndex < 0)
        {
            return null;
        }

        var fragmentStartIndex = redirectUri.IndexOf('#', queryStartIndex);
        return fragmentStartIndex >= 0
            ? redirectUri[queryStartIndex..fragmentStartIndex]
            : redirectUri[queryStartIndex..];
    }

    private static bool IsSafeLocalUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl)
               && returnUrl[0] == '/'
               && !returnUrl.StartsWith("//", StringComparison.Ordinal)
               && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
    }
}
