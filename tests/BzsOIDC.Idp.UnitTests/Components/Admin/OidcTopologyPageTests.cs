using System.Security.Claims;
using Bunit;
using BzsOIDC.Idp.Components.Admin;
using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Idp.Services.Oidc;
using BzsOIDC.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Components.Admin;

public sealed class OidcTopologyPageTests
{
    [Fact]
    public void Render_WhenAdminAccessGranted_ShowsClientAndScopeTopology()
    {
        using var context = CreateContext();

        var clientService = Substitute.For<IOidcClientService>();
        clientService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OidcClientResponse>>([
                new OidcClientResponse
                {
                    ClientId = "client-1",
                    DisplayName = "Client 1",
                    AuthFlow = OidcClientAuthFlow.AuthorizationCode,
                    Scopes = ["api"],
                },
            ]));

        var scopeService = Substitute.For<IOidcScopeService>();
        scopeService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OidcScopeResponse>>([
                new OidcScopeResponse { Name = "api", DisplayName = "API", Resources = ["resource"] },
            ]));

        var permissionCatalogService = Substitute.For<IPermissionCatalogService>();
        permissionCatalogService.GetResourcesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProtectedResourceResponse>>([
                new ProtectedResourceResponse
                {
                    Key = "api",
                    Permissions = [new PermissionDefinitionResponse { Name = "clients.read", ReleaseScopes = ["api"] }],
                },
            ]));

        context.Services.AddSingleton<IOidcClientService>(clientService);
        context.Services.AddSingleton<IOidcScopeService>(scopeService);
        context.Services.AddSingleton<IPermissionCatalogService>(permissionCatalogService);
        context.Services.AddSingleton<IStringLocalizer<ScopeManagement>, TestStringLocalizer<ScopeManagement>>();
        context.Services.AddSingleton<IHttpContextAccessor>(CreateAdminHttpContextAccessor());

        var cut = context.Render<OidcTopology>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Client 1", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("api", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("clients.read", cut.Markup, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(5));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    private static IHttpContextAccessor CreateAdminHttpContextAccessor()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(ClaimTypes.Role, "admin"),
            ],
            "TestAuth");

        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
    }
}

