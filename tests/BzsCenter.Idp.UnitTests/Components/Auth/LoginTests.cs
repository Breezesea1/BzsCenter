using System.Security.Claims;
using Bunit;
using BzsCenter.Idp.Components.Auth.Pages.Account;
using BzsCenter.Idp.Components.Auth.Shared;
using BzsCenter.Idp.Services.Identity;
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
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
            ], "TestAuth"))));
        context.Services.AddSingleton<IStringLocalizer<Login>, TestStringLocalizer<Login>>();
        context.Services.AddSingleton<IStringLocalizer<AuthPreferences>, TestStringLocalizer<AuthPreferences>>();
        context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();
        context.Services.AddSingleton<IExternalLoginProviderStore>(new EmptyExternalLoginProviderStore());

        var navigationManager = context.Services.GetRequiredService<NavigationManager>();

        context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<Login>());

        Assert.Equal("http://localhost/", navigationManager.Uri);
    }

    [Fact]
    public void Login_WhenGitHubProviderEnabled_RendersGitHubLoginForm()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity())));
        context.Services.AddSingleton<IStringLocalizer<Login>, TestStringLocalizer<Login>>();
        context.Services.AddSingleton<IStringLocalizer<AuthPreferences>, TestStringLocalizer<AuthPreferences>>();
        context.Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();
        context.Services.AddSingleton<IExternalLoginProviderStore>(new TestExternalLoginProviderStore());

        var cut = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<Login>());

        var externalLoginForm = cut.Find("form[action='/account/external-login/github']");
        Assert.Contains("LoginWithExternalProvider", externalLoginForm.TextContent);
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

    private sealed class TestExternalLoginProviderStore : IExternalLoginProviderStore
    {
        public IReadOnlyList<ExternalLoginProvider> GetEnabledProviders()
        {
            return [new ExternalLoginProvider(ExternalLoginProvider.GitHubRouteSegment, ExternalLoginProvider.GitHubScheme, "GitHub")];
        }

        public bool TryGetProvider(string routeSegment, out ExternalLoginProvider provider)
        {
            provider = new ExternalLoginProvider(ExternalLoginProvider.GitHubRouteSegment, ExternalLoginProvider.GitHubScheme, "GitHub");
            return string.Equals(routeSegment, ExternalLoginProvider.GitHubRouteSegment, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class EmptyExternalLoginProviderStore : IExternalLoginProviderStore
    {
        public IReadOnlyList<ExternalLoginProvider> GetEnabledProviders()
        {
            return [];
        }

        public bool TryGetProvider(string routeSegment, out ExternalLoginProvider provider)
        {
            provider = new ExternalLoginProvider(string.Empty, string.Empty, string.Empty);
            return false;
        }
    }
}
