using System.Security.Claims;
using Bunit;
using BzsCenter.Idp.Components.Auth.Pages.Account;
using BzsCenter.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BzsCenter.Idp.UnitTests.Components.Auth;

public sealed class LoginTests
{
    [Fact]
    public void Login_WhenAuthenticated_RedirectsToHome()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
            ], "TestAuth"))));
        context.Services.AddSingleton<IStringLocalizer<Login>, TestStringLocalizer<Login>>();
        context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();

        var navigationManager = context.Services.GetRequiredService<NavigationManager>();

        context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<Login>());

        Assert.Equal("http://localhost/", navigationManager.Uri);
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state = new(user);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(_state);
        }
    }

    private sealed class TestAntiforgeryStateProvider : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken? GetAntiforgeryToken()
        {
            return new AntiforgeryRequestToken("test-request-token", "test-form-field");
        }
    }
}
