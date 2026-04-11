using BzsOIDC.Idp.Services.Identity;

namespace BzsOIDC.Idp.UnitTests.Services.Identity;

public sealed class ExternalAuthenticationFailureResponseBuilderTests
{
    [Theory]
    [InlineData("Correlation failed.", "/login?error=external_login_expired&returnUrl=%2Fadmin%2Fusers")]
    [InlineData("error=access_denied", "/login?error=external_login_access_denied&returnUrl=%2Fadmin%2Fusers")]
    [InlineData("Something else failed.", "/login?error=external_login_failed&returnUrl=%2Fadmin%2Fusers")]
    public void BuildLoginRedirectPath_MapsRemoteFailureMessageToExpectedErrorCode(string failureMessage, string expected)
    {
        var redirectPath = ExternalAuthenticationFailureResponseBuilder.BuildLoginRedirectPath(
            new InvalidOperationException(failureMessage),
            "/account/external-login/callback?returnUrl=%2Fadmin%2Fusers");

        Assert.Equal(expected, redirectPath);
    }

    [Fact]
    public void BuildLoginRedirectPath_WhenRedirectUriContainsUnsafeReturnUrl_DropsReturnUrl()
    {
        var redirectPath = ExternalAuthenticationFailureResponseBuilder.BuildLoginRedirectPath(
            new InvalidOperationException("Correlation failed."),
            "/account/external-login/callback?returnUrl=https%3A%2F%2Fevil.example");

        Assert.Equal("/login?error=external_login_expired", redirectPath);
    }

    [Fact]
    public void BuildLoginRedirectPath_WhenRedirectUriContainsFragment_PreservesReturnUrlFromQuery()
    {
        var redirectPath = ExternalAuthenticationFailureResponseBuilder.BuildLoginRedirectPath(
            new InvalidOperationException("Correlation failed."),
            "/account/external-login/callback?returnUrl=%2Fadmin%2Fusers#provider-error");

        Assert.Equal("/login?error=external_login_expired&returnUrl=%2Fadmin%2Fusers", redirectPath);
    }
}
