using BzsOIDC.Idp.Services.Identity;
using Microsoft.Extensions.Options;

namespace BzsOIDC.Idp.UnitTests.Services.Identity;

public sealed class ExternalLoginProviderStoreTests
{
    [Fact]
    public void GetEnabledProviders_WhenGitHubEnabled_ReturnsGitHubDescriptor()
    {
        var sut = CreateSut(new ExternalAuthenticationOptions
        {
            GitHub = new GitHubExternalAuthenticationOptions
            {
                ClientId = "gho_example_valid_client_id",
                ClientSecret = "ghs_example_valid_client_secret",
            },
        });

        var providers = sut.GetEnabledProviders();

        var provider = Assert.Single(providers);
        Assert.Equal(ExternalLoginProvider.GitHubRouteSegment, provider.RouteSegment);
        Assert.Equal(ExternalLoginProvider.GitHubScheme, provider.Scheme);
        Assert.Equal("GitHub", provider.DisplayName);
        Assert.True(sut.TryGetProvider(ExternalLoginProvider.GitHubRouteSegment, out var resolvedProvider));
        Assert.Equal(provider, resolvedProvider);
    }

    [Fact]
    public void GetEnabledProviders_WhenGitHubDisabled_ReturnsEmptyCollection()
    {
        var sut = CreateSut(new ExternalAuthenticationOptions());

        var providers = sut.GetEnabledProviders();

        Assert.Empty(providers);
        Assert.False(sut.TryGetProvider(ExternalLoginProvider.GitHubRouteSegment, out _));
    }

    [Fact]
    public void GetEnabledProviders_WhenGitHubUsesPlaceholderCredentials_ReturnsEmptyCollection()
    {
        var sut = CreateSut(new ExternalAuthenticationOptions
        {
            GitHub = new GitHubExternalAuthenticationOptions
            {
                ClientId = "github-client-id",
                ClientSecret = "github-client-secret",
            },
        });

        var providers = sut.GetEnabledProviders();

        Assert.Empty(providers);
        Assert.False(sut.TryGetProvider(ExternalLoginProvider.GitHubRouteSegment, out _));
    }

    private static ExternalLoginProviderStore CreateSut(ExternalAuthenticationOptions options)
    {
        return new ExternalLoginProviderStore(Options.Create(options));
    }
}
