using BzsOIDC.Idp.Services.Oidc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Services.Oidc;

public sealed class OidcConsentPageRendererTests
{
    [Fact]
    public void Render_WhenPathBasePresent_UsesPathBaseAwareFormAction()
    {
        var antiforgery = Substitute.For<IAntiforgery>();
        antiforgery.GetAndStoreTokens(Arg.Any<HttpContext>())
            .Returns(new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", "X-CSRF-TOKEN"));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = "/auth";
        httpContext.Request.Path = "/connect/authorize";
        var renderer = new OidcConsentPageRenderer(antiforgery);

        var result = renderer.Render(
            httpContext,
            [new KeyValuePair<string, StringValues>("client_id", "client")],
            "Client",
            ["openid"]);

        Assert.Contains("action=\"/auth/connect/authorize\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("name=\"client_id\" value=\"client\"", result.Content, StringComparison.Ordinal);
    }
}
