using System.Net;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace BzsOIDC.Idp.Services.Oidc;

public interface IOidcConsentPageRenderer
{
    ContentResult Render(
        HttpContext httpContext,
        IEnumerable<KeyValuePair<string, StringValues>> query,
        string clientDisplayName,
        IReadOnlyList<string> scopes);
}

internal sealed class OidcConsentPageRenderer(IAntiforgery antiforgery) : IOidcConsentPageRenderer
{
    // Keep this as a protocol-owned minimal document: the authorization endpoint must be able to
    // return a consent form without depending on Blazor circuit state or client-side assets.
    public ContentResult Render(
        HttpContext httpContext,
        IEnumerable<KeyValuePair<string, StringValues>> query,
        string clientDisplayName,
        IReadOnlyList<string> scopes)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        var formAction = httpContext.Request.PathBase.Add(httpContext.Request.Path).Value ?? "/connect/authorize";
        var builder = new StringBuilder();

        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("<title>Authorize application - BzsOIDC</title>");
        builder.AppendLine("<style>body{font-family:system-ui,-apple-system,Segoe UI,sans-serif;margin:0;background:#0f172a;color:#e2e8f0}.consent-page{min-height:100vh;display:grid;place-items:center;padding:2rem}.consent-card{width:min(44rem,100%);background:#111827;border:1px solid #334155;border-radius:1rem;padding:2rem;box-shadow:0 24px 80px rgba(0,0,0,.35)}.scope-list{display:flex;flex-wrap:wrap;gap:.5rem;margin:1rem 0}.scope{background:#1e293b;border:1px solid #475569;border-radius:999px;padding:.35rem .75rem}.actions{display:flex;gap:.75rem;margin-top:1.5rem}.primary,.secondary{border:0;border-radius:.75rem;padding:.75rem 1rem;font-weight:700}.primary{background:#38bdf8;color:#082f49}.secondary{background:#334155;color:#f8fafc}</style>");
        builder.AppendLine("</head><body><main class=\"consent-page\"><section class=\"consent-card\">");
        builder.Append("<p>BzsOIDC authorization request</p><h1>Allow ")
            .Append(WebUtility.HtmlEncode(clientDisplayName))
            .AppendLine(" to access your account?</h1>");
        builder.AppendLine("<p>The application is requesting the following scopes.</p><div class=\"scope-list\">");

        foreach (var scope in scopes)
        {
            builder.Append("<span class=\"scope\">")
                .Append(WebUtility.HtmlEncode(scope))
                .AppendLine("</span>");
        }

        builder.Append("</div><form method=\"post\" action=\"")
            .Append(WebUtility.HtmlEncode(formAction))
            .AppendLine("\">");
        builder.Append("<input name=\"")
            .Append(WebUtility.HtmlEncode(tokens.FormFieldName))
            .Append("\" type=\"hidden\" value=\"")
            .Append(WebUtility.HtmlEncode(tokens.RequestToken))
            .AppendLine("\">");

        foreach (var pair in query)
        {
            foreach (var value in pair.Value)
            {
                builder.Append("<input type=\"hidden\" name=\"")
                    .Append(WebUtility.HtmlEncode(pair.Key))
                    .Append("\" value=\"")
                    .Append(WebUtility.HtmlEncode(value))
                    .AppendLine("\">");
            }
        }

        builder.AppendLine("<div class=\"actions\"><button class=\"primary\" type=\"submit\" name=\"consent\" value=\"accept\">Allow access</button>");
        builder.AppendLine("<button class=\"secondary\" type=\"submit\" name=\"consent\" value=\"deny\">Deny</button></div>");
        builder.AppendLine("</form></section></main></body></html>");

        return new ContentResult
        {
            Content = builder.ToString(),
            ContentType = "text/html; charset=utf-8",
        };
    }
}
