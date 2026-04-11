using System.Security.Claims;
using Bunit;
using BzsOIDC.Idp.Components.Admin;
using BzsOIDC.Idp.Services.Oidc;
using BzsOIDC.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Components.Admin;

public sealed class ScopeManagementPageTests
{
    [Fact]
    public void SearchInput_FiltersMatchingScopes()
    {
        using var context = CreateContext();

        var scopes = new[]
        {
            new OidcScopeResponse { Name = "api.read", DisplayName = "Read API", Resources = ["api"] },
            new OidcScopeResponse { Name = "api.write", DisplayName = "Write API", Resources = ["api"] },
        };

        var service = Substitute.For<IOidcScopeService>();
        service.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OidcScopeResponse>>(scopes));

        context.Services.AddSingleton(service);
        context.Services.AddSingleton<IStringLocalizer<ScopeManagement>, TestStringLocalizer<ScopeManagement>>();
        context.Services.AddSingleton<IHttpContextAccessor>(CreateAdminHttpContextAccessor());

        var cut = context.Render<ScopeManagement>();

        cut.WaitForAssertion(() => Assert.Contains("api.read", cut.Markup, StringComparison.Ordinal));

        cut.Find("#scope-search").Input("write");

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("api.read", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("api.write", cut.Markup, StringComparison.Ordinal);
        });
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./Components/Admin/AdminDialogShell.razor.js")
            .SetupVoid("activate", _ => true);
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
