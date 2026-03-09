using BzsCenter.Idp.Services.Oidc;
using OpenIddict.Abstractions;

namespace BzsCenter.Idp.UnitTests.Services.Oidc;

public class OidcClientDescriptorFactoryTests
{
    [Fact]
    public void CreateDescriptor_CreatesConfidentialClientWithGeneratedSecretAndPermissions()
    {
        var request = new OidcClientUpsertRequest
        {
            DisplayName = "Test Confidential Client",
            PublicClient = false,
            ClientSecret = null,
            GrantTypes = [OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken],
            Scopes = ["api", OpenIddictConstants.Scopes.OpenId],
            RedirectUris = ["https://localhost:6001/signin-oidc"],
            PostLogoutRedirectUris = ["https://localhost:6001/signout-callback-oidc"],
        };

        var descriptor = OidcClientDescriptorFactory.CreateDescriptor(request, "client-confidential");

        Assert.Equal("client-confidential", descriptor.ClientId);
        Assert.Equal(OpenIddictConstants.ClientTypes.Confidential, descriptor.ClientType);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ClientSecret));
        Assert.Matches("^[0-9A-F]{64}$", descriptor.ClientSecret!);
        Assert.Contains(OpenIddictConstants.Permissions.Prefixes.GrantType + OpenIddictConstants.GrantTypes.AuthorizationCode,
            descriptor.Permissions);
        Assert.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            descriptor.Permissions);
        Assert.Contains(OpenIddictConstants.Permissions.Endpoints.Authorization, descriptor.Permissions);
        Assert.Contains(OpenIddictConstants.Permissions.Endpoints.Token, descriptor.Permissions);
        Assert.Contains(OpenIddictConstants.Permissions.Endpoints.EndSession, descriptor.Permissions);
        Assert.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange, descriptor.Requirements);
        Assert.Single(descriptor.RedirectUris);
        Assert.Single(descriptor.PostLogoutRedirectUris);
    }

    [Fact]
    public void CreateDescriptor_CreatesPublicClientWithoutSecret()
    {
        var request = new OidcClientUpsertRequest
        {
            DisplayName = "Public Client",
            PublicClient = true,
            RequireProofKeyForCodeExchange = true,
            GrantTypes = [OpenIddictConstants.GrantTypes.AuthorizationCode],
            Scopes = ["api"],
        };

        var descriptor = OidcClientDescriptorFactory.CreateDescriptor(request, "client-public");

        Assert.Equal(OpenIddictConstants.ClientTypes.Public, descriptor.ClientType);
        Assert.Null(descriptor.ClientSecret);
        Assert.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange, descriptor.Requirements);
        Assert.Contains(OpenIddictConstants.Permissions.Endpoints.Authorization, descriptor.Permissions);
        Assert.Contains(OpenIddictConstants.Permissions.Endpoints.Token, descriptor.Permissions);
        Assert.DoesNotContain(OpenIddictConstants.Permissions.Endpoints.Revocation, descriptor.Permissions);
    }

    [Fact]
    public void BuildPermissions_ForClientCredentials_DoesNotIncludeAuthorizationEndpoint()
    {
        var request = new OidcClientUpsertRequest
        {
            DisplayName = "Machine Client",
            PublicClient = false,
            GrantTypes = [OpenIddictConstants.GrantTypes.ClientCredentials],
            Scopes = ["api"],
        };

        var permissions = OidcClientDescriptorFactory.BuildPermissions(request);

        Assert.Contains(OpenIddictConstants.Permissions.Endpoints.Token, permissions);
        Assert.DoesNotContain(OpenIddictConstants.Permissions.Endpoints.Authorization, permissions);
        Assert.DoesNotContain(OpenIddictConstants.Permissions.ResponseTypes.Code, permissions);
    }

    [Fact]
    public void ValidateRequest_ReturnsErrors_ForInvalidAuthorizationCodeClient()
    {
        var request = new OidcClientUpsertRequest
        {
            DisplayName = string.Empty,
            PublicClient = true,
            ClientSecret = "should-not-exist",
            GrantTypes = [OpenIddictConstants.GrantTypes.AuthorizationCode],
            RedirectUris = ["bad-uri"],
        };

        var errors = OidcClientDescriptorFactory.ValidateRequest(request);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, static e => e.Contains("DisplayName is required", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors,
            static e => e.Contains("Public clients must not specify ClientSecret", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, static e => e.Contains("Invalid URI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateDescriptor_WhenGeneratingSecret_CreatesUniqueCryptographicHexSecret()
    {
        var request = new OidcClientUpsertRequest
        {
            DisplayName = "Confidential Client",
            PublicClient = false,
            GrantTypes = [OpenIddictConstants.GrantTypes.ClientCredentials],
            Scopes = ["api"],
        };

        var first = OidcClientDescriptorFactory.CreateDescriptor(request, "client-1");
        var second = OidcClientDescriptorFactory.CreateDescriptor(request, "client-2");

        Assert.NotNull(first.ClientSecret);
        Assert.NotNull(second.ClientSecret);
        Assert.Matches("^[0-9A-F]{64}$", first.ClientSecret);
        Assert.Matches("^[0-9A-F]{64}$", second.ClientSecret);
        Assert.NotEqual(first.ClientSecret, second.ClientSecret);
    }
}
