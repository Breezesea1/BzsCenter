using System.Security.Claims;
using BzsCenter.Idp.Models;
using BzsCenter.Idp.Services.Identity;
using BzsCenter.Idp.Services.Oidc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenIddict.Abstractions;

namespace BzsCenter.Idp.UnitTests.Services.Oidc;

public sealed class OidcPrincipalFactoryTests
{
    [Fact]
    public void FilterRequestedScopes_RemovesUnconfiguredScopesAndDuplicates()
    {
        var sut = CreateSut();

        var scopes = sut.FilterRequestedScopes([
            OpenIddictConstants.Scopes.OpenId,
            "api",
            "API",
            "unknown",
        ]);

        Assert.Equal([OpenIddictConstants.Scopes.OpenId, "api"], scopes);
    }

    [Fact]
    public async Task CreateUserPrincipalAsync_RewritesStandardClaimsToOpenIddictClaims()
    {
        var user = BzsUser.CreateExternal("octocat", "octocat@users.noreply.github.com", "The Octocat");
        var userManager = Substitute.For<UserManager<BzsUser>>(
            Substitute.For<IUserStore<BzsUser>>(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<BzsUser>(),
            Array.Empty<IUserValidator<BzsUser>>(),
            Array.Empty<IPasswordValidator<BzsUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<UserManager<BzsUser>>>());
        var signInManager = Substitute.For<SignInManager<BzsUser>>(
            userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<BzsUser>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<SignInManager<BzsUser>>>(),
            Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<BzsUser>>());

        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.Name, "legacy-name"));
        identity.AddClaim(new Claim(ClaimTypes.Email, "legacy@example.com"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "admin"));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, "admin"));
        signInManager.CreateUserPrincipalAsync(user).Returns(new ClaimsPrincipal(identity));
        userManager.GetUserIdAsync(user).Returns("user-1");
        userManager.GetRolesAsync(user).Returns(Task.FromResult<IList<string>>(["admin", "operator"]));

        var sut = new OidcPrincipalFactory(signInManager, userManager, Options.Create(new IdentitySeedOptions()));

        var principal = await sut.CreateUserPrincipalAsync(user);
        var resultIdentity = Assert.Single(principal.Identities);

        Assert.Equal("user-1", principal.GetClaim(OpenIddictConstants.Claims.Subject));
        Assert.Equal("The Octocat", principal.GetClaim(OpenIddictConstants.Claims.Name));
        Assert.Equal("octocat@users.noreply.github.com", principal.GetClaim(OpenIddictConstants.Claims.Email));
        Assert.DoesNotContain(resultIdentity.Claims, static claim => claim.Type == ClaimTypes.Name);
        Assert.DoesNotContain(resultIdentity.Claims, static claim => claim.Type == ClaimTypes.Email);
        Assert.DoesNotContain(resultIdentity.Claims, static claim => claim.Type == ClaimTypes.Role);
        Assert.Equal(["admin", "operator"], resultIdentity.FindAll(OpenIddictConstants.Claims.Role)
            .Select(static claim => claim.Value)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    [Fact]
    public void CreateClientPrincipal_WhenDisplayNameProvided_UsesClientIdentityClaims()
    {
        var sut = CreateSut();

        var principal = sut.CreateClientPrincipal("machine-client", "Machine Client");

        Assert.Equal("machine-client", principal.GetClaim(OpenIddictConstants.Claims.Subject));
        Assert.Equal("Machine Client", principal.GetClaim(OpenIddictConstants.Claims.Name));
    }

    [Fact]
    public async Task CreateUserPrincipalAsync_WhenDisplayNameMissing_FallsBackToUserName()
    {
        var user = BzsUser.CreateExternal("octocat", "octocat@users.noreply.github.com", null);
        var userManager = Substitute.For<UserManager<BzsUser>>(
            Substitute.For<IUserStore<BzsUser>>(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<BzsUser>(),
            Array.Empty<IUserValidator<BzsUser>>(),
            Array.Empty<IPasswordValidator<BzsUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<UserManager<BzsUser>>>());
        var signInManager = Substitute.For<SignInManager<BzsUser>>(
            userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<BzsUser>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<SignInManager<BzsUser>>>(),
            Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<BzsUser>>());

        signInManager.CreateUserPrincipalAsync(user).Returns(new ClaimsPrincipal(new ClaimsIdentity("test")));
        userManager.GetUserIdAsync(user).Returns("user-1");
        userManager.GetRolesAsync(user).Returns(Task.FromResult<IList<string>>([]));

        var sut = new OidcPrincipalFactory(signInManager, userManager, Options.Create(new IdentitySeedOptions()));

        var principal = await sut.CreateUserPrincipalAsync(user);

        Assert.Equal("octocat", principal.GetClaim(OpenIddictConstants.Claims.Name));
    }

    private static OidcPrincipalFactory CreateSut()
    {
        var userManager = Substitute.For<UserManager<BzsUser>>(
            Substitute.For<IUserStore<BzsUser>>(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<BzsUser>(),
            Array.Empty<IUserValidator<BzsUser>>(),
            Array.Empty<IPasswordValidator<BzsUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<UserManager<BzsUser>>>());
        var signInManager = Substitute.For<SignInManager<BzsUser>>(
            userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<BzsUser>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<SignInManager<BzsUser>>>(),
            Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<BzsUser>>());

        return new OidcPrincipalFactory(signInManager, userManager, Options.Create(new IdentitySeedOptions()));
    }
}
