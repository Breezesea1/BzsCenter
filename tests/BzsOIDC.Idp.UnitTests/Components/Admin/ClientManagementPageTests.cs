using System.Security.Claims;
using Bunit;
using Bunit.JSInterop;
using BzsOIDC.Idp.Components.Admin;
using BzsOIDC.Idp.Services.Oidc;
using BzsOIDC.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Components.Admin;

public sealed class ClientManagementPageTests
{
    [Fact]
    public void PageSizeChange_RecomputesVisibleRows_AndNextPageShowsRemainingClients()
    {
        using var context = CreateContext();

        var clients = Enumerable.Range(1, 22)
            .Select(index => new OidcClientResponse
            {
                ClientId = $"client-{index:00}",
                DisplayName = $"Client {index:00}",
                AuthFlow = index % 2 == 0 ? OidcClientAuthFlow.AuthorizationCode : OidcClientAuthFlow.ClientCredentials,
                GrantTypes = [index % 2 == 0 ? "authorization_code" : "client_credentials"],
                Scopes = [$"scope-{index:00}"],
                RedirectUris = index % 2 == 0 ? [$"https://app{index:00}.example.com/signin"] : []
            })
            .ToArray();

        var clientService = Substitute.For<IOidcClientService>();
        clientService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<OidcClientResponse>>(clients));

        context.Services.AddSingleton(clientService);
        context.Services.AddSingleton<IStringLocalizer<ClientManagement>, TestStringLocalizer<ClientManagement>>();
        context.Services.AddSingleton<IHttpContextAccessor>(CreateAdminHttpContextAccessor());

        var cut = context.Render<ClientManagement>();

        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll("tbody tr").Count));

        cut.Find("#client-page-size").Change("20");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(20, cut.FindAll("tbody tr").Count);
            Assert.Contains("client-20", cut.Markup, StringComparison.Ordinal);
        });

        cut.FindAll("button").Single(button => button.TextContent.Contains("NextPage", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll("tbody tr").Count);
            Assert.Contains("client-21", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("client-22", cut.Markup, StringComparison.Ordinal);
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
                new Claim(ClaimTypes.Role, "admin")
            ],
            "TestAuth");

        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
