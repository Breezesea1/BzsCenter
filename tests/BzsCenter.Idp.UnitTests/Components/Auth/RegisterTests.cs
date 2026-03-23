using System.Security.Claims;
using Bunit;
using BzsCenter.Idp.Components.Auth.Pages.Account;
using BzsCenter.Idp.Components.Auth.Shared;
using BzsCenter.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BzsCenter.Idp.UnitTests.Components.Auth;

public sealed class RegisterTests
{
    [Fact]
    public void Register_WhenAuthenticated_RedirectsToHome()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
            ], "TestAuth"))));
        context.Services.AddSingleton<IStringLocalizer<Register>, TestStringLocalizer<Register>>();
        context.Services.AddSingleton<IStringLocalizer<AuthPreferences>, TestStringLocalizer<AuthPreferences>>();
        context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();

        var navigationManager = context.Services.GetRequiredService<NavigationManager>();

        context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<Register>());

        Assert.Equal("http://localhost/", navigationManager.Uri);
    }

    [Fact]
    public void Register_RendersStandaloneRegistrationFormAndLoginLink()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity())));
        context.Services.AddSingleton<IStringLocalizer<Register>, TestStringLocalizer<Register>>();
        context.Services.AddSingleton<IStringLocalizer<AuthPreferences>, TestStringLocalizer<AuthPreferences>>();
        context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();

        var cut = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<Register>());

        var registerForm = cut.Find("form.register-form[action='/account/register']");
        Assert.Contains("register-username", registerForm.InnerHtml);
        Assert.Contains("register-email", registerForm.InnerHtml);
        Assert.Contains("register-confirm-password", registerForm.InnerHtml);

        var signInLink = cut.Find(".signup-tip a");
        Assert.Equal("/login", signInLink.GetAttribute("href"));
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
