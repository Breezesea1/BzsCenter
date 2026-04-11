using Microsoft.AspNetCore.WebUtilities;

namespace BzsCenter.Idp.Services.Identity;

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

        var uri = Uri.TryCreate(redirectUri, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri($"https://localhost{redirectUri}", UriKind.Absolute);

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("returnUrl", out var returnUrl))
        {
            return null;
        }

        var candidate = returnUrl.ToString();
        return IsSafeLocalUrl(candidate) ? candidate : null;
    }

    private static bool IsSafeLocalUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl)
               && returnUrl[0] == '/'
               && !returnUrl.StartsWith("//", StringComparison.Ordinal)
               && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
    }
}
