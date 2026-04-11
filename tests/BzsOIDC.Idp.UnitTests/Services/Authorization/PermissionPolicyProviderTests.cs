using BzsOIDC.Idp.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BzsOIDC.Idp.UnitTests.Services.Authorization;

public class PermissionPolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_BuildsPermissionPolicy_ForPrefixedPolicyName()
    {
        var provider = CreateProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync("perm:users.write");

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, requirement => requirement.GetType().Name == "PermissionRequirement");
    }

    [Fact]
    public async Task GetPolicyAsync_UsesFallback_ForUnprefixedPolicyName()
    {
        var options = new AuthorizationOptions();
        options.AddPolicy("regular", p => p.RequireAuthenticatedUser());

        var provider = CreateProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync("regular");

        Assert.NotNull(policy);
        Assert.NotEmpty(policy.Requirements);
    }

    [Fact]
    public async Task GetPolicyAsync_StillSupportsDefaultPrefix_WhenConfiguredPrefixChanges()
    {
        var provider = CreateProvider(
            Options.Create(new AuthorizationOptions()),
            Options.Create(new PermissionPolicyOptions { PolicyPrefix = "custom:" }));

        var policy = await provider.GetPolicyAsync("perm:users.write");

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, requirement => requirement.GetType().Name == "PermissionRequirement");
    }

    private static IAuthorizationPolicyProvider CreateProvider(IOptions<AuthorizationOptions> authorizationOptions)
    {
        return CreateProvider(authorizationOptions, Options.Create(new PermissionPolicyOptions()));
    }

    private static IAuthorizationPolicyProvider CreateProvider(
        IOptions<AuthorizationOptions> authorizationOptions,
        IOptions<PermissionPolicyOptions> permissionPolicyOptions)
    {
        var providerType = typeof(PermissionAuthorizationHandler).Assembly
            .GetType("BzsOIDC.Idp.Services.Authorization.PermissionPolicyProvider", throwOnError: true)!;

        var instance = Activator.CreateInstance(
            providerType,
            authorizationOptions,
            permissionPolicyOptions);

        Assert.NotNull(instance);
        return (IAuthorizationPolicyProvider)instance!;
    }
}
