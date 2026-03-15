using System.Security.Claims;
using Bunit;
using BzsCenter.Idp.Client.Components.Layout;
using BzsCenter.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BzsCenter.Idp.UnitTests.Components.Layout;

public sealed class NavMenuTests
{
    [Fact]
    public void NavMenu_WhenAuthenticated_RendersCurrentUserIdentity()
    {
        using var context = CreateContext();
        SetAuthenticationState(
            context,
            new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "readonly-user"),
            new Claim(ClaimTypes.Email, "readonly-user@bzscenter.local"),
        ], "TestAuth")));

        var cut = RenderNavMenu(context);

        Assert.Contains("readonly-user", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("readonly-user@bzscenter.local", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("admin@bzscenter.com", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("/login", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_WhenAnonymous_RendersGuestFooter()
    {
        using var context = CreateContext();
        SetAuthenticationState(context, new ClaimsPrincipal(new ClaimsIdentity()));

        var cut = RenderNavMenu(context);

        Assert.Contains("Guest", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Sign in to continue", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("/login", cut.Markup, StringComparison.Ordinal);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddAuthorization();
        context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        context.Services.AddSingleton<IStringLocalizer<NavMenu>, TestStringLocalizer<NavMenu>>();
        return context;
    }

    private static IRenderedComponent<CascadingAuthenticationState> RenderNavMenu(BunitContext context)
    {
        return context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NavMenu>());
    }

    private static void SetAuthenticationState(BunitContext context, ClaimsPrincipal user)
    {
        context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(user));
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state = new(user);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(_state);
        }
    }

    private sealed class AllowAllAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(user.Identity?.IsAuthenticated == true
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName)
        {
            return Task.FromResult(user.Identity?.IsAuthenticated == true
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }
    }
}
